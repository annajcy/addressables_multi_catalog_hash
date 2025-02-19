using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Editor.Extenstion.Build.MultiCatalogHash.AlternativeIP;
using Editor.Extenstion.Build.MultiCatalogHash.Tools;
using Unity.Plastic.Newtonsoft.Json;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEngine.Build.Pipeline;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.Serialization;
using static UnityEditor.AddressableAssets.Build.ContentUpdateScript;
using Object = System.Object;


namespace Editor.Extenstion.Build.MultiCatalogHash.Core
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Build scripts used for player builds and running with bundles in the editor.
    /// </summary>
    [CreateAssetMenu(fileName = "MultiCatalogHashBuild.asset", menuName = "Addressables/Content Builders/Multi Catalog Hash Build Script")]
    public sealed class MultiCatalogHashBuild : BuildScriptBase
    {
        /// <inheritdoc />
        public override string Name => "Multi Catalog Hash Build Script";
        public List<ExternalCatalog> externalCatalogs = new List<ExternalCatalog>();
        public bool buildDefaultCatalogInRemote = true;
        public bool generateBundlesInFolders = true;
        public string alternativeIpsUrl = "http://127.0.0.1:8085/remote_ips.json";
        public string buildResultCacheLoadUrl = "http://127.0.0.1:8085/build_cache.json";
        public string buildResultCacheSavePath = "ServerData/build_cache.json";

        private string catalogBuildPath = string.Empty;
        private HashSet<string> createdProviderIds = new HashSet<string>();
        private LinkXmlGenerator linker = LinkXmlGenerator.CreateDefault();
        private List<ObjectInitializationData> resourceProviderData = new List<ObjectInitializationData>();
        private List<AssetBundleBuild> allBundleInputDefs = new List<AssetBundleBuild>();
        private readonly List<CatalogSetup> catalogSetups = new List<CatalogSetup>();
        private readonly Dictionary<string, string> bundleToInternalId = new Dictionary<string, string>();
        private Dictionary<string, List<ContentCatalogDataEntry>> primaryKeyToDependers = new Dictionary<string, List<ContentCatalogDataEntry>>();
        private Dictionary<string, ContentCatalogDataEntry> primaryKeyToLocation = new Dictionary<string, ContentCatalogDataEntry>();
        private Dictionary<AddressableAssetGroup, (string, string)[]> groupToBundleNames = new Dictionary<AddressableAssetGroup, (string, string)[]>();

        /// <summary>
        /// A temporary list of the groups that get processed during a build.
        /// </summary>
        private readonly List<AddressableAssetGroup> includedGroupsInBuild = new List<AddressableAssetGroup>();

        // Tests can set this flag to prevent player script compilation. This is the most expensive part of small builds
        // and isn't needed for most tests.
        private static readonly bool skipCompilePlayerScripts = false;

        private Dictionary<string, List<ContentCatalogDataEntry>> GetPrimaryKeyToDependerLocations(List<ContentCatalogDataEntry> locations)
        {
            if (primaryKeyToDependers != null)
                return primaryKeyToDependers;
            if (locations == null || locations.Count == 0)
            {
                Debug.LogError("Attempting to get Entries dependent on key, but currently no locations");
                return new Dictionary<string, List<ContentCatalogDataEntry>>(0);
            }

            primaryKeyToDependers = new Dictionary<string, List<ContentCatalogDataEntry>>(locations.Count);
            foreach (ContentCatalogDataEntry location in locations)
            {
                for (int i = 0; i < location.Dependencies.Count; ++i)
                {
                    string dependencyKey = location.Dependencies[i] as string;
                    if (string.IsNullOrEmpty(dependencyKey))
                        continue;

                    if (!primaryKeyToDependers.TryGetValue(dependencyKey, out var dependers))
                    {
                        dependers = new List<ContentCatalogDataEntry>();
                        primaryKeyToDependers.Add(dependencyKey, dependers);
                    }

                    dependers.Add(location);
                }
            }

            return primaryKeyToDependers;
        }

        private Dictionary<string, ContentCatalogDataEntry> GetPrimaryKeyToLocation(List<ContentCatalogDataEntry> locations)
        {
            if (primaryKeyToLocation != null)
                return primaryKeyToLocation;
            if (locations == null || locations.Count == 0)
            {
                Debug.LogError("Attempting to get Primary key to entries dependent on key, but currently no locations");
                return new Dictionary<string, ContentCatalogDataEntry>();
            }

            primaryKeyToLocation = new Dictionary<string, ContentCatalogDataEntry>();
            foreach (var loc in locations)
            {
                if (loc != null && loc.Keys[0] != null && loc.Keys[0] is string && !primaryKeyToLocation.ContainsKey((string)loc.Keys[0]))
                    primaryKeyToLocation[(string)loc.Keys[0]] = loc;
            }

            return primaryKeyToLocation;
        }

        /// <inheritdoc />
        public override bool CanBuildData<T>()
        {
            return typeof(T).IsAssignableFrom(typeof(AddressablesPlayerBuildResult));
        }

        /// <inheritdoc />
        protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput builderInput)
        {
            NotifyUserAboutBuildReport();

            TResult result = default(TResult);
            includedGroupsInBuild?.Clear();

            InitializeBuildContext(builderInput, out AddressableAssetsBuildContext aaContext);

            using (Log.ScopedStep(LogLevel.Info, "ProcessAllGroups"))
            {
                var errorString = ProcessAllGroups(aaContext);
                if (!string.IsNullOrEmpty(errorString))
                    result = CreateErrorResult<TResult>(errorString, builderInput, aaContext);
            }

            result ??= DoBuild<TResult>(builderInput, aaContext);

            if (result == null)
                return default;

            var span = DateTime.Now - aaContext.buildStartTime;
            result.Duration = span.TotalSeconds;
            if (string.IsNullOrEmpty(result.Error))
            {
                ClearContentUpdateNotifications(includedGroupsInBuild);
            }
            DisplayBuildReport();
            return result;
        }

        private TResult CreateErrorResult<TResult>(string errorString, AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext) where TResult : IDataBuilderResult
        {
            BuildLayoutGenerationTask.GenerateErrorReport(errorString, aaContext, builderInput.PreviousContentState);
            return AddressableAssetBuildResult.CreateResult<TResult>(null, 0, errorString);
        }

        private void InitializeBuildContext(AddressablesDataBuilderInput builderInput, out AddressableAssetsBuildContext aaContext)
        {
            var now = DateTime.Now;
            var aaSettings = builderInput.AddressableSettings;
#if ENABLE_CCD
            // we have to populate the ccd managed data every time we build.
            try
            {
                CcdBuildEvents.Instance.PopulateCcdManagedData(aaSettings, aaSettings.activeProfileId);
            }
            catch (Exception e)
            {
                Addressables.LogError("Unable to populated CCD Managed Data. You may need to refresh remote data in the profile window.");
                throw;
            }
#endif
            allBundleInputDefs = new List<AssetBundleBuild>();
            groupToBundleNames = new Dictionary<AddressableAssetGroup, (string, string)[]>();
            // force these caches to be rebuilt
            primaryKeyToDependers = null;
            primaryKeyToLocation = null;
            var bundleToAssetGroup = new Dictionary<string, string>();
            var runtimeData = new ResourceManagerRuntimeData
            {
                SettingsHash = aaSettings.currentHash.ToString(),
                CertificateHandlerType = aaSettings.CertificateHandlerType,
                BuildTarget = builderInput.Target.ToString(),
#if ENABLE_CCD
                CcdManagedData = aaSettings.m_CcdManagedData,
#endif
                LogResourceManagerExceptions = aaSettings.buildSettings.LogResourceManagerExceptions,
                DisableCatalogUpdateOnStartup = aaSettings.DisableCatalogUpdateOnStartup,
#if ENABLE_JSON_CATALOG
                IsLocalCatalogInBundle = aaSettings.BundleLocalCatalog,
#endif
                AddressablesVersion = Addressables.Version,
                MaxConcurrentWebRequests = aaSettings.MaxConcurrentWebRequests,
                CatalogRequestsTimeout = aaSettings.CatalogRequestsTimeout
            };
            linker = UnityEditor.Build.Pipeline.Utilities.LinkXmlGenerator.CreateDefault();
            linker.AddAssemblies(new[] {typeof(Addressables).Assembly, typeof(UnityEngine.ResourceManagement.ResourceManager).Assembly});
            linker.AddTypes(runtimeData.CertificateHandlerType);

            resourceProviderData = new List<ObjectInitializationData>();
            aaContext = new AddressableAssetsBuildContext
            {
                Settings = aaSettings,
                runtimeData = runtimeData,
                bundleToAssetGroup = bundleToAssetGroup,
                locations = new List<ContentCatalogDataEntry>(),
                providerTypes = new HashSet<Type>(),
                assetEntries = new List<AddressableAssetEntry>(),
                buildStartTime = now
            };

            createdProviderIds = new HashSet<string>();
        }

        struct SBPSettingsOverwriterScope : IDisposable
        {
            bool m_PrevSlimResults;

            public SBPSettingsOverwriterScope(bool forceFullWriteResults)
            {
                m_PrevSlimResults = ScriptableBuildPipeline.slimWriteResults;
                if (forceFullWriteResults)
                    ScriptableBuildPipeline.slimWriteResults = false;
            }

            public void Dispose()
            {
                ScriptableBuildPipeline.slimWriteResults = m_PrevSlimResults;
            }
        }

        private static string GetBuiltInBundleNamePrefix(AddressableAssetsBuildContext aaContext)
        {
            return GetBuiltInBundleNamePrefix(aaContext.Settings);
        }

        private static string GetBuiltInBundleNamePrefix(AddressableAssetSettings settings)
        {
            string value = "";
            switch (settings.BuiltInBundleNaming)
            {
                case BuiltInBundleNaming.DefaultGroupGuid:
                    value = settings.DefaultGroup.Guid;
                    break;
                case BuiltInBundleNaming.ProjectName:
                    value = Hash128.Compute(GetProjectName()).ToString();
                    break;
                case BuiltInBundleNaming.Custom:
                    value = settings.BuiltInBundleCustomNaming;
                    break;
            }

            return value;
        }

        private void AddBundleProvider(BundledAssetGroupSchema schema)
        {
            var bundleProviderId = schema.GetBundleCachedProviderId();

            if (!createdProviderIds.Add(bundleProviderId)) return;
            var bundleProviderType = schema.AssetBundleProviderType.Value;
            var bundleProviderData = ObjectInitializationData.CreateSerializedInitializationData(bundleProviderType, bundleProviderId);
            resourceProviderData.Add(bundleProviderData);
        }

        private static string GetMonoScriptBundleNamePrefix(AddressableAssetsBuildContext aaContext)
        {
            return GetMonoScriptBundleNamePrefix(aaContext.Settings);
        }

        private static string GetMonoScriptBundleNamePrefix(AddressableAssetSettings settings)
        {
            string value = null;
            switch (settings.MonoScriptBundleNaming)
            {
                case MonoScriptBundleNaming.ProjectName:
                    value = Hash128.Compute(GetProjectName()).ToString();
                    break;
                case MonoScriptBundleNaming.DefaultGroupGuid:
                    value = settings.DefaultGroup.Guid;
                    break;
                case MonoScriptBundleNaming.Custom:
                    value = settings.MonoScriptBundleCustomNaming;
                    break;
            }

            return value;
        }

        private CatalogBuildInfo GetDefaultCatalog(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext)
        {
            var catalog = new CatalogBuildInfo(ResourceManagerRuntimeData.kCatalogAddress,
                builderInput.RuntimeCatalogFilename.Split('.').First())
                {
                    buildPath = Addressables.BuildPath,
                    loadPath = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}"
                };
            catalog.locations.AddRange(aaContext.locations);
            return catalog;
        }

        private List<CatalogBuildInfo> GetContentCatalogs(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext)
		{
			// cleanup
			catalogSetups.Clear();

			// Prepare catalogs
			var defaultCatalog = GetDefaultCatalog(builderInput, aaContext);
			defaultCatalog.locations.Clear(); // This will get filled up again below, but filtered by external catalog setups.

			foreach (ExternalCatalog externalCatalog in externalCatalogs)
				if (externalCatalog != null)
					catalogSetups.Add(new CatalogSetup(externalCatalog, builderInput, aaContext));

			foreach (var dataEntry in aaContext.locations)
			{
				var preferredCatalog = catalogSetups.FirstOrDefault(cs => cs.externalCatalog.IsPartOfCatalog(dataEntry, aaContext));
                var fileName = Utility.GetFileName(dataEntry.InternalId, builderInput.Target);
                if (preferredCatalog != null)
                {

                    if (generateBundlesInFolders && dataEntry.Dependencies.Count == 0)
                    {
                        Utility.MoveFile(preferredCatalog.catalogBuildInfo.rootBuildPath + $"/{fileName}" ,
                            preferredCatalog.catalogBuildInfo.buildPath);
                        dataEntry.InternalId = preferredCatalog.catalogBuildInfo.loadPath +
                                               $"/{fileName}";
                    }

                    preferredCatalog.catalogBuildInfo.locations.Add(dataEntry);
					if (dataEntry.ResourceType == typeof(IAssetBundleResource))
						preferredCatalog.catalogBuildInfo.includedBundles.Add(fileName);
				}
				else
                {
                    defaultCatalog.locations.Add(dataEntry);
                    if (dataEntry.ResourceType == typeof(IAssetBundleResource))
                        defaultCatalog.includedBundles.Add(fileName);
                }
			}

			foreach (CatalogSetup catalogSetup in catalogSetups)
			{
				var locationQueue = new Queue<ContentCatalogDataEntry>(catalogSetup.catalogBuildInfo.locations);
				var processedLocations = new HashSet<ContentCatalogDataEntry>();

				while (locationQueue.Count > 0)
				{
					ContentCatalogDataEntry location = locationQueue.Dequeue();

					// If the location has already been processed, or doesn't have any dependencies, then skip it.
                    if (!processedLocations.Add(location) || location.Dependencies == null || location.Dependencies.Count == 0)
						continue;

					foreach (var entryDependency in location.Dependencies)
					{
						// Search for the dependencies in the default catalog only.
						var dependencyLocation = defaultCatalog.locations.Find(loc => loc.Keys[0] == entryDependency);

						if (dependencyLocation != null)
						{
							locationQueue.Enqueue(dependencyLocation);

							// If the dependency wasn't part of the catalog yet, add it.
							if (!catalogSetup.catalogBuildInfo.locations.Contains(dependencyLocation))
								catalogSetup.catalogBuildInfo.locations.Add(dependencyLocation);
						}
						else if (!catalogSetup.catalogBuildInfo.locations.Exists(loc => loc.Keys[0] == entryDependency))
                            Debug.LogErrorFormat("Could not find location for dependency ID {0} in the default catalog.", entryDependency);
					}
				}
			}

			// Gather catalogs
			var catalogs = new List<CatalogBuildInfo>(catalogSetups.Count + 1) { defaultCatalog };
            foreach (var setup in catalogSetups)
				if (!setup.IsEmpty)
					catalogs.Add(setup.catalogBuildInfo);

			return catalogs;
		}

        public static void BuildAlternativeRemoteIPCatalogCommandLine()
        {
            string buildResultCacheLoadPath = Utility.GetCommandLineArg("-buildResultCacheLoadUrl");
            string alternativeRemoteIPLoadUrl = Utility.GetCommandLineArg("-alternativeRemoteIPLoadUrl");
            BuildAlternativeRemoteIPCatalog(buildResultCacheLoadPath, alternativeRemoteIPLoadUrl);
        }

        public static void BuildAlternativeRemoteIPCatalog(string buildResultCacheLoadUrl, string alternativeRemoteIPLoadUrl)
        {
            string buildCacheJson = Utility.DownloadJsonFromUrl(buildResultCacheLoadUrl);
            string remoteIpsJson = Utility.DownloadJsonFromUrl(alternativeRemoteIPLoadUrl);
            var buildResultCache = AddressablesBuildResultCache.LoadFromJson(buildCacheJson);
            var remoteIps = RemoteIPList.LoadFromJson(remoteIpsJson);

            if (buildResultCache != null && remoteIps != null && remoteIps.Count != 0)
            {
                BuildAlternativeRemoteIPCatalog(
                    buildResultCache.builderInput.ToOriginal(buildResultCache.aaContext.ToOriginal().Settings),
                    buildResultCache.aaContext.ToOriginal(),
                    buildResultCache.buildResult.ToOriginal(),
                    buildResultCache.buildInfos.ToOriginal(),
                    buildResultCache.resourceProviderDataList.ToOriginal(),
                    Type.GetType(buildResultCache.sceneProvider),
                    Type.GetType(buildResultCache.instanceProvider),
                    remoteIps);
                Debug.Log("Build Alternative Remote IP Catalog Performed for MultiCatalogHashBuild.");
            }
            else Debug.LogError("Failed To Build Alternative Remote IP Catalog Performed for MultiCatalogHashBuild.");
        }

        private static void BuildAlternativeRemoteIPCatalog
            (AddressablesDataBuilderInput builderInput,
                AddressableAssetsBuildContext aaContext,
                AddressablesPlayerBuildResult addrResult,
                List<CatalogBuildInfo> catalogs,
                List<ObjectInitializationData> resourceProviderData,
                Type sceneProvider,
                Type instanceProvider,
                List<RemoteIP> remoteIps)
        {
            catalogs.RemoveAt(0);
            catalogs.ForEach(catalogInfo =>
            {
                var buildPath = catalogInfo.buildPath;
                var loadPath = catalogInfo.loadPath;

                foreach (var ip in remoteIps)
                {
                    foreach (var catalogDataEntry in catalogInfo.locations)
                    {
                        if (catalogDataEntry.Dependencies.Count != 0) continue;
                        catalogDataEntry.InternalId = Utility.ReplaceIPAddress(
                            catalogDataEntry.InternalId,
                            ip.Address);
                    }

                    catalogInfo.loadPath = Utility.ReplaceIPAddress(loadPath, ip.Address);
                    catalogInfo.buildPath = buildPath + "_" + ip.identifier;

                    var contentCatalog = new ContentCatalogData(catalogInfo.identifier);

                    # region Generate Caxtalog Files

                    if (addrResult != null)
                    {
                        List<Object> hashingObjects = new List<Object>();
                        foreach (var assetBundleBuildResult in addrResult.AssetBundleBuildResults)
                        {
                            if (catalogInfo.includedBundles.Exists(bundleName => string.Equals(bundleName,
                                    Utility.GetFileName(assetBundleBuildResult.FilePath, builderInput.Target))))
                                hashingObjects.Add(assetBundleBuildResult.Hash);
                        }
                        string buildResultHash = HashingMethods.Calculate(hashingObjects.ToArray()).ToString();
                        contentCatalog.BuildResultHash = buildResultHash;
                    }

                    //set data
                    contentCatalog.SetData(catalogInfo.locations.OrderBy(entry => entry.InternalId).ToList());

                    //set providers
                    contentCatalog.ResourceProviderData.AddRange(resourceProviderData);
                    foreach (var t in aaContext.providerTypes)
                        contentCatalog.ResourceProviderData.Add(ObjectInitializationData
                            .CreateSerializedInitializationData(t));
                    contentCatalog.InstanceProviderData =
                        ObjectInitializationData.CreateSerializedInitializationData(instanceProvider);
                    contentCatalog.SceneProviderData =
                        ObjectInitializationData.CreateSerializedInitializationData(sceneProvider);

#if ENABLE_JSON_CATALOG

                    //save json catalog
                    string jsonText = JsonUtility.ToJson(contentCatalog);

                    if (aaContext.Settings.BuildRemoteCatalog || ProjectConfigData.GenerateBuildLayout)
                    {
                        string contentHash = HashingMethods.Calculate(jsonText).ToString();
                        contentCatalog.LocalHash = contentHash;
                    }

                    CreateCatalogFilesStatic(jsonText, catalogInfo, builderInput, aaContext);
#else
                    //save binary catalog
                    byte[] bytes = contentCatalog.SerializeToByteArray();

                    if (aaContext.Settings.BuildRemoteCatalog || ProjectConfigData.GenerateBuildLayout)
                    {
                        string contentHash = HashingMethods.Calculate(bytes).ToString();
                        contentCatalog.LocalHash = contentHash;
                    }

                    CreateCatalogFilesStatic(bytes, catalogInfo, builderInput, aaContext);
#endif
                    #endregion
                }

                catalogInfo.buildPath = buildPath;
                catalogInfo.loadPath = loadPath;
            });
        }

        /// <summary>
        /// The method that does the actual building after all the groups have been processed.
        /// </summary>
        /// <param name="builderInput">The generic builderInput of the</param>
        /// <param name="aaContext"></param>
        /// <typeparam name="TResult"></typeparam>
        /// <returns></returns>
        private TResult DoBuild<TResult>(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext) where TResult : IDataBuilderResult
        {
            #region Prepare

            var genericResult = AddressableAssetBuildResult.CreateResult<TResult>();
            AddressablesPlayerBuildResult addrResult = genericResult as AddressablesPlayerBuildResult;

            ExtractDataTask extractData = new ExtractDataTask();
            ContentUpdateContext contentUpdateContext = default;
            List<CachedAssetState> carryOverCachedState = new List<CachedAssetState>();
            var tempPath = Path.GetDirectoryName(Application.dataPath) + "/" + Addressables.LibraryPath + PlatformMappingService.GetPlatformPathSubFolder() + "/addressables_content_state.bin";
            var bundleRenameMap = new Dictionary<string, string>();
            var playerBuildVersion = builderInput.PlayerVersion;

            #endregion

            #region Build Bundle

            if (allBundleInputDefs.Count > 0)
            {
                if (!BuildUtility.CheckModifiedScenesAndAskToSave())
                    return CreateErrorResult<TResult>("Unsaved scenes", builderInput, aaContext);

                var buildTarget = builderInput.Target;
                var buildTargetGroup = builderInput.TargetGroup;

                var buildParams = new AddressableAssetsBundleBuildParameters(
                    aaContext.Settings,
                    aaContext.bundleToAssetGroup,
                    buildTarget,
                    buildTargetGroup,
                    aaContext.Settings.buildSettings.bundleBuildPath);

                var builtinBundleName = GetBuiltInBundleNamePrefix(aaContext) + $"{BuiltInBundleBaseName}.bundle";

                var schema = aaContext.Settings.DefaultGroup.GetSchema<BundledAssetGroupSchema>();
                AddBundleProvider(schema);

                string monoScriptBundleName = GetMonoScriptBundleNamePrefix(aaContext);
                if (!string.IsNullOrEmpty(monoScriptBundleName))
                    monoScriptBundleName += "_monoscripts.bundle";
                var buildTasks = RuntimeDataBuildTasks(builtinBundleName, monoScriptBundleName);
                buildTasks.Add(extractData);

                IBundleBuildResults results;
                using (Log.ScopedStep(LogLevel.Info, "ContentPipeline.BuildAssetBundles"))
                using (new SBPSettingsOverwriterScope(ProjectConfigData.GenerateBuildLayout)) // build layout generation requires full SBP write results
                {
                    var buildContent = new BundleBuildContent(allBundleInputDefs);
                    var exitCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out results, buildTasks, aaContext, Log);

                    if (exitCode < ReturnCode.Success)
                        return CreateErrorResult<TResult>("SBP Error" + exitCode, builderInput, aaContext);
                }

                var postCatalogUpdateCallbacks = new List<Action>();
                var groups = aaContext.Settings.groups.Where(g => g != null);
                var addressableAssetGroups = groups.ToList();
                using (Log.ScopedStep(LogLevel.Info, "PostProcessBundles"))
                using (var progressTracker = new UnityEditor.Build.Pipeline.Utilities.ProgressTracker())
                {
                    progressTracker.UpdateTask("Post Processing AssetBundles");

                    AddressableAssetGroup sharedBundleGroup = aaContext.Settings.GetSharedBundleGroup();
                    foreach (var assetGroup in addressableAssetGroups)
                    {
                        if (!aaContext.assetGroupToBundles.ContainsKey(assetGroup))
                            continue;

                        using (Log.ScopedStep(LogLevel.Info, assetGroup.name))
                        {
                            PostProcessBundles(assetGroup, results, addrResult,
                                builderInput.Registry, aaContext,
                                bundleRenameMap, postCatalogUpdateCallbacks, sharedBundleGroup);
                        }
                    }
                }

                using (Log.ScopedStep(LogLevel.Info, "Process Catalog Entries"))
                {
                    Dictionary<string, ContentCatalogDataEntry> locationIdToCatalogEntryMap = BuildLocationIdToCatalogEntryMap(aaContext.locations);
                    if (builderInput.PreviousContentState != null)
                    {
                        contentUpdateContext = new ContentUpdateContext()
                        {
                            BundleToInternalBundleIdMap = bundleToInternalId,
                            GuidToPreviousAssetStateMap = BuildGuidToCachedAssetStateMap(builderInput.PreviousContentState, aaContext.Settings),
                            IdToCatalogDataEntryMap = locationIdToCatalogEntryMap,
                            WriteData = extractData.WriteData,
                            ContentState = builderInput.PreviousContentState,
                            Registry = builderInput.Registry,
                            PreviousAssetStateCarryOver = carryOverCachedState
                        };
                    }
                    ProcessCatalogEntriesForBuild(aaContext, addressableAssetGroups, builderInput, extractData.WriteData,
                        contentUpdateContext, bundleToInternalId, locationIdToCatalogEntryMap);
                    foreach (var postUpdateCatalogCallback in postCatalogUpdateCallbacks)
                        postUpdateCatalogCallback.Invoke();

                    foreach (var r in results.WriteResults)
                    {
                        var resultValue = r.Value;
                        linker.AddTypes(resultValue.includedTypes);
                        linker.AddSerializedClass(resultValue.includedSerializeReferenceFQN);
                    }
                }
            }



            #endregion

            #region Build Catalog

            var catalogs = GetContentCatalogs(builderInput, aaContext);

            var instanceProvider = instanceProviderType.Value;
            var sceneProvider = sceneProviderType.Value;

            var buildResultCache = new AddressablesBuildResultCache(builderInput, aaContext, addrResult, catalogs, resourceProviderData, sceneProvider, instanceProvider);
            buildResultCache.SaveToJson(buildResultCacheSavePath);

            catalogs.ForEach(catalogInfo =>
            {
                var contentCatalog = new ContentCatalogData(catalogInfo.identifier);

                # region Generate Caxtalog Files

                string catalogType =

#if ENABLE_JSON_CATALOG
                    "Json";
#else
                    "bin";
#endif

                using (Log.ScopedStep(LogLevel.Info, $"Generate {catalogType} Catalog"))
                {
                    if (addrResult != null)
                    {
                        List<Object> hashingObjects = new List<Object>();
                        foreach (var assetBundleBuildResult in addrResult.AssetBundleBuildResults)
                        {
                            if (catalogInfo.includedBundles.Exists(bundleName => string.Equals(bundleName,
                                    Utility.GetFileName(assetBundleBuildResult.FilePath, builderInput.Target))))
                                hashingObjects.Add(assetBundleBuildResult.Hash);
                        }

                        string buildResultHash = HashingMethods.Calculate(hashingObjects.ToArray()).ToString();
                        contentCatalog.BuildResultHash = buildResultHash;
                    }

                    //set data
                    contentCatalog.SetData(catalogInfo.locations.OrderBy(entry => entry.InternalId).ToList());

                    //set providers
                    contentCatalog.ResourceProviderData.AddRange(resourceProviderData);
                    foreach (var t in aaContext.providerTypes)
                        contentCatalog.ResourceProviderData.Add(ObjectInitializationData
                            .CreateSerializedInitializationData(t));
                    contentCatalog.InstanceProviderData =
                        ObjectInitializationData.CreateSerializedInitializationData(instanceProviderType.Value);
                    contentCatalog.SceneProviderData =
                        ObjectInitializationData.CreateSerializedInitializationData(sceneProviderType.Value);

#if ENABLE_JSON_CATALOG

                    //save json catalog
                    string jsonText = null;
                    using (Log.ScopedStep(LogLevel.Info, "Generating Json"))
                        jsonText = JsonUtility.ToJson(contentCatalog);

                    if (aaContext.Settings.BuildRemoteCatalog || ProjectConfigData.GenerateBuildLayout)
                    {
                        string contentHash = null;
                        using (Log.ScopedStep(LogLevel.Info, "Hashing Catalog"))
                            contentHash = HashingMethods.Calculate(jsonText).ToString();
                        contentCatalog.LocalHash = contentHash;
                    }

                    CreateCatalogFiles(jsonText, catalogInfo, builderInput, aaContext);
#else
                    //save binary catalog
                    byte[] bytes = null;
                    using (Log.ScopedStep(LogLevel.Info, "Generating Bin"))
                        bytes = contentCatalog.SerializeToByteArray();

                    if (aaContext.Settings.BuildRemoteCatalog || ProjectConfigData.GenerateBuildLayout)
                    {
                        string contentHash = null;
                        using (Log.ScopedStep(LogLevel.Info, "Hashing Catalog"))
                            contentHash = HashingMethods.Calculate(bytes).ToString();
                        contentCatalog.LocalHash = contentHash;
                    }

                    CreateCatalogFiles(bytes, catalogInfo, builderInput, aaContext);
#endif
                }

                #endregion

            });

            #endregion

            #region Generate Link

            using (Log.ScopedStep(LogLevel.Info, $"Generate link"))
            {
                var contentCatalog = new ContentCatalogData();
                //set providers
                contentCatalog.ResourceProviderData.AddRange(resourceProviderData);
                foreach (var t in aaContext.providerTypes)
                    contentCatalog.ResourceProviderData.Add(ObjectInitializationData.CreateSerializedInitializationData(t));
                contentCatalog.InstanceProviderData = ObjectInitializationData.CreateSerializedInitializationData(instanceProviderType.Value);
                contentCatalog.SceneProviderData = ObjectInitializationData.CreateSerializedInitializationData(sceneProviderType.Value);

                foreach (var pd in contentCatalog.ResourceProviderData)
                {
                    linker.AddTypes(pd.ObjectType.Value);
                    linker.AddTypes(pd.GetRuntimeTypes());
                }

                linker.AddTypes(contentCatalog.InstanceProviderData.ObjectType.Value);
                linker.AddTypes(contentCatalog.InstanceProviderData.GetRuntimeTypes());
                linker.AddTypes(contentCatalog.SceneProviderData.ObjectType.Value);
                linker.AddTypes(contentCatalog.SceneProviderData.GetRuntimeTypes());

                foreach (var io in aaContext.Settings.InitializationObjects)
                {
                    if (io is IObjectInitializationDataProvider provider)
                    {
                        var id = provider.CreateObjectInitializationData();
                        aaContext.runtimeData.InitializationObjects.Add(id);
                        linker.AddTypes(id.ObjectType.Value);
                        linker.AddTypes(id.GetRuntimeTypes());
                    }
                }

                linker.AddTypes(typeof(Addressables));
                Directory.CreateDirectory(Addressables.BuildPath + "/AddressablesLink/");
                linker.Save(Addressables.BuildPath + $"/AddressablesLink/link.xml");
            }

            #endregion

            #region Generate Settings

            var settingsPath = Addressables.BuildPath + "/" + builderInput.RuntimeSettingsFilename;
            using (Log.ScopedStep(LogLevel.Info, "Generate Settings"))
                WriteFile(settingsPath, JsonUtility.ToJson(aaContext.runtimeData), builderInput.Registry);

            #endregion

            #region Generate Content Update State

            if (extractData.BuildCache != null && builderInput.PreviousContentState == null)
            {
                using (Log.ScopedStep(LogLevel.Info, "Generate Content Update State"))
                {
                    var remoteCatalogLoadPath = aaContext.Settings.BuildRemoteCatalog
                        ? aaContext.Settings.RemoteCatalogLoadPath.GetValue(aaContext.Settings)
                        : string.Empty;

                    var allEntries = new List<AddressableAssetEntry>();
                    using (Log.ScopedStep(LogLevel.Info, "Get Assets"))
                        aaContext.Settings.GetAllAssets(allEntries, false, ContentUpdateScript.GroupFilterFunc);

                    if (ContentUpdateScript.SaveContentState(aaContext.locations, aaContext.GuidToCatalogLocation, tempPath, allEntries,
                            extractData.DependencyData, playerBuildVersion, remoteCatalogLoadPath,
                            carryOverCachedState))
                    {
                        string contentStatePath = ContentUpdateScript.GetContentStateDataPath(false, aaContext.Settings);
                        if (ResourceManagerConfig.ShouldPathUseWebRequest(contentStatePath))
                        {
#if ENABLE_CCD
                            contentStatePath = Path.Combine(aaContext.Settings.RemoteCatalogBuildPath.GetValue(aaContext.Settings), Path.GetFileName(tempPath));
#else
                            contentStatePath = ContentUpdateScript.PreviousContentStateFileCachePath;
#endif
                        }

                        CopyAndRegisterContentState(tempPath, contentStatePath, builderInput, addrResult);
                    }
                }
            }

            if (addrResult != null)
                addrResult.IsUpdateContentBuild = builderInput.PreviousContentState != null;

            genericResult.LocationCount = aaContext.locations.Count;
            genericResult.OutputPath = settingsPath;

            #endregion

            #region GenerateBuildLayout

            if (ProjectConfigData.GenerateBuildLayout && extractData.BuildContext != null)
            {
                using var progressTracker = new ProgressTracker();
                progressTracker.UpdateTask("Generating Build Layout");
                using (Log.ScopedStep(LogLevel.Info, "Generate Build Layout"))
                {
                    List<IBuildTask> tasks = new List<IBuildTask>();
                    var buildLayoutTask = new BuildLayoutGenerationTask();

                    List<ContentCatalogDataEntry> locations = new List<ContentCatalogDataEntry>();
                    foreach (var catalogBuildInfo in catalogs)
                        locations.AddRange(catalogBuildInfo.locations);

                    extractData.BuildContext.SetContextObject<IBuildLayoutParameters>(
                        new BuildLayoutParameters(bundleRenameMap, new ContentCatalogData(locations)));

                    tasks.Add(buildLayoutTask);
                    BuildTasksRunner.Run(tasks, extractData.BuildContext);
                }
            }

            #endregion

            return genericResult;
        }

        private static void ProcessCatalogEntriesForBuild(AddressableAssetsBuildContext aaContext,
            IEnumerable<AddressableAssetGroup> validGroups, AddressablesDataBuilderInput builderInput, IBundleWriteData writeData,
            ContentUpdateContext contentUpdateContext, Dictionary<string, string> bundleToInternalId, Dictionary<string, ContentCatalogDataEntry> locationIdToCatalogEntryMap)
        {
            using (var progressTracker = new UnityEditor.Build.Pipeline.Utilities.ProgressTracker())
            {
                progressTracker.UpdateTask("Post Processing Catalog Entries");
                if (builderInput.PreviousContentState != null)
                {
                    RevertUnchangedAssetsToPreviousAssetState.Run(aaContext, contentUpdateContext);
                }
                else
                {
                    foreach (var assetGroup in validGroups)
                        SetAssetEntriesBundleFileIdToCatalogEntryBundleFileId(assetGroup.entries, bundleToInternalId, writeData, locationIdToCatalogEntryMap);
                }
            }

            bundleToInternalId.Clear();
        }

        private static Dictionary<string, ContentCatalogDataEntry> BuildLocationIdToCatalogEntryMap(List<ContentCatalogDataEntry> locations)
        {
            Dictionary<string, ContentCatalogDataEntry> locationIdToCatalogEntryMap = new Dictionary<string, ContentCatalogDataEntry>();
            foreach (var location in locations)
                locationIdToCatalogEntryMap[location.InternalId] = location;

            return locationIdToCatalogEntryMap;
        }

        private static Dictionary<string, CachedAssetState> BuildGuidToCachedAssetStateMap(AddressablesContentState contentState, AddressableAssetSettings settings)
        {
            Dictionary<string, CachedAssetState> addressableEntryToCachedStateMap = new Dictionary<string, CachedAssetState>();
            foreach (var cachedInfo in contentState.cachedInfos)
                addressableEntryToCachedStateMap[cachedInfo.asset.guid.ToString()] = cachedInfo;

            return addressableEntryToCachedStateMap;
        }

        private static bool CreateCatalogFilesStatic(
#if ENABLE_JSON_CATALOG
            string jsonText,
#else
            byte[] data,
#endif
             CatalogBuildInfo catalogBuildInfo, AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext)
        {
            string identifier = catalogBuildInfo.identifier;
            string fileName = catalogBuildInfo.fileName;
            if (
#if ENABLE_JSON_CATALOG
                string.IsNullOrEmpty(jsonText)
#else
                data == null
#endif
                || builderInput == null || aaContext == null || identifier == null || fileName == null)
            {
                Addressables.LogError("Unable to create content catalog (Null arguments).");
                return false;
            }

            // Path needs to be resolved at runtime.
            string localLoadPath = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/" +
                                   Utility.AppendFileNameExtension(fileName);

#if ENABLE_JSON_CATALOG
            var catalogHash = HashingMethods.Calculate(jsonText);
#else
            var catalogHash = HashingMethods.Calculate(data);
#endif

            var catalogBuildPath = Path.Combine(Addressables.BuildPath,
                Utility.AppendFileNameExtension(fileName));

#if ENABLE_JSON_CATALOG
            if (aaContext.Settings.BundleLocalCatalog)
            {
                localLoadPath = localLoadPath.Replace(".json", ".bundle");
                catalogBuildPath = catalogBuildPath.Replace(".json", ".bundle");
                var returnCode = CreateCatalogBundle(catalogBuildPath, jsonText, builderInput);
                if (returnCode != ReturnCode.Success || !File.Exists(catalogBuildPath))
                {
                    Addressables.LogError($"An error occured during the creation of the content catalog bundle (return code {returnCode}).");
                    return false;
                }
            }
            else
            {
                if (catalogBuildInfo.IsDefaultCatalog || catalogBuildInfo.registerToSettings)
                {
                    WriteFile(catalogBuildPath, jsonText, builderInput.Registry);
                    WriteFile(catalogBuildPath.Replace(".json", ".hash"), catalogHash.ToString(), builderInput.Registry);
                }
            }
#else
            if (catalogBuildInfo.IsDefaultCatalog || catalogBuildInfo.registerToSettings)
            {
                WriteFile(catalogBuildPath, data, builderInput.Registry);
                WriteFile(catalogBuildPath.Replace(".bin", ".hash"), catalogHash.ToString(), builderInput.Registry);
            }

#endif
            string[] dependencyHashes = null;

            if (aaContext.Settings.BuildRemoteCatalog)
            {
                if (!catalogBuildInfo.IsDefaultCatalog)
                {
                    dependencyHashes = CreateRemoteCatalog(
#if ENABLE_JSON_CATALOG
                    jsonText,
#else
                        data,
#endif
                        aaContext.runtimeData.CatalogLocations,
                        aaContext.Settings,
                        builderInput,
                        new ProviderLoadRequestOptions
                            {IgnoreFailures = true},
                        catalogHash.ToString(),
                        catalogBuildInfo);
                }
            }

            ResourceLocationData targetCatalog = new ResourceLocationData(
                new[] {identifier},
                localLoadPath,
                typeof(ContentCatalogProvider),
                typeof(ContentCatalogData),
                dependencyHashes)
            {
                //We need to set the data here because this location data gets used later if we decide to load the remote/cached catalog instead.  See DetermineIdToLoad(...)
                Data = new ProviderLoadRequestOptions { IgnoreFailures = true }
            };

            if (catalogBuildInfo.registerToSettings) aaContext.runtimeData.CatalogLocations.Add(targetCatalog);
            return true;
        }

        private bool CreateCatalogFiles(
#if ENABLE_JSON_CATALOG
            string jsonText,
#else
            byte[] data,
#endif
             CatalogBuildInfo catalogBuildInfo, AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext)
        {
            string identifier = catalogBuildInfo.identifier;
            string fileName = catalogBuildInfo.fileName;
            if (
#if ENABLE_JSON_CATALOG
                string.IsNullOrEmpty(jsonText)
#else
                data == null
#endif
                || builderInput == null || aaContext == null || identifier == null || fileName == null)
            {
                Addressables.LogError("Unable to create content catalog (Null arguments).");
                return false;
            }

            // Path needs to be resolved at runtime.
            string localLoadPath = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/" +
                                   Utility.AppendFileNameExtension(fileName);

#if ENABLE_JSON_CATALOG
            var catalogHash = HashingMethods.Calculate(jsonText);
#else
            var catalogHash = HashingMethods.Calculate(data);
#endif

            catalogBuildPath = Path.Combine(Addressables.BuildPath,
                Utility.AppendFileNameExtension(fileName));

#if ENABLE_JSON_CATALOG
            if (aaContext.Settings.BundleLocalCatalog)
            {
                localLoadPath = localLoadPath.Replace(".json", ".bundle");
                catalogBuildPath = catalogBuildPath.Replace(".json", ".bundle");
                var returnCode = CreateCatalogBundle(catalogBuildPath, jsonText, builderInput);
                if (returnCode != ReturnCode.Success || !File.Exists(catalogBuildPath))
                {
                    Addressables.LogError($"An error occured during the creation of the content catalog bundle (return code {returnCode}).");
                    return false;
                }
            }
            else
            {
                if (catalogBuildInfo.IsDefaultCatalog || catalogBuildInfo.registerToSettings)
                {
                    WriteFile(catalogBuildPath, jsonText, builderInput.Registry);
                    WriteFile(catalogBuildPath.Replace(".json", ".hash"), catalogHash.ToString(), builderInput.Registry);
                }
            }
#else
            if (catalogBuildInfo.IsDefaultCatalog || catalogBuildInfo.registerToSettings)
            {
                WriteFile(catalogBuildPath, data, builderInput.Registry);
                WriteFile(catalogBuildPath.Replace(".bin", ".hash"), catalogHash.ToString(), builderInput.Registry);
            }

#endif
            string[] dependencyHashes = null;

            if (aaContext.Settings.BuildRemoteCatalog)
            {
                if (!catalogBuildInfo.IsDefaultCatalog ||
                    (catalogBuildInfo.IsDefaultCatalog && buildDefaultCatalogInRemote) )
                {
                    dependencyHashes = CreateRemoteCatalog(
#if ENABLE_JSON_CATALOG
                    jsonText,
#else
                        data,
#endif
                        aaContext.runtimeData.CatalogLocations,
                        aaContext.Settings,
                        builderInput,
                        new ProviderLoadRequestOptions
                            {IgnoreFailures = true},
                        catalogHash.ToString(),
                        catalogBuildInfo);
                }
            }

            ResourceLocationData targetCatalog = new ResourceLocationData(
                new[] {identifier},
                localLoadPath,
                typeof(ContentCatalogProvider),
                typeof(ContentCatalogData),
                dependencyHashes)
            {
                //We need to set the data here because this location data gets used later if we decide to load the remote/cached catalog instead.  See DetermineIdToLoad(...)
                Data = new ProviderLoadRequestOptions { IgnoreFailures = true }
            };

            if (catalogBuildInfo.registerToSettings) aaContext.runtimeData.CatalogLocations.Add(targetCatalog);
            return true;
        }

#if ENABLE_JSON_CATALOG
        private static ReturnCode CreateCatalogBundle(string filepath, string jsonText, AddressablesDataBuilderInput builderInput)
        {
            if (string.IsNullOrEmpty(filepath) || string.IsNullOrEmpty(jsonText) || builderInput == null)
            {
                throw new ArgumentException("Unable to create catalog bundle (null arguments).");
            }

            // A bundle requires an actual asset
            var tempFolderName = "TempCatalogFolder";

            var configFolder = AddressableAssetSettingsDefaultObject.kDefaultConfigFolder;
            if (builderInput.AddressableSettings != null && builderInput.AddressableSettings.IsPersisted)
                configFolder = builderInput.AddressableSettings.ConfigFolder;

            var tempFolderPath = Path.Combine(configFolder, tempFolderName);
            var tempFilePath = Path.Combine(tempFolderPath, Path.GetFileName(filepath).Replace(".bundle", ".json"));
            if (!WriteFile(tempFilePath, jsonText, builderInput.Registry))
            {
                throw new Exception("An error occured during the creation of temporary files needed to bundle the content catalog.");
            }

            AssetDatabase.Refresh();

            var bundleBuildContent = new BundleBuildContent(new[]
            {
                new AssetBundleBuild()
                {
                    assetBundleName = Path.GetFileName(filepath),
                    assetNames = new[] {tempFilePath},
                    addressableNames = Array.Empty<string>()
                }
            });

            var buildTasks = new List<IBuildTask>
            {
                new CalculateAssetDependencyData(),
                new GenerateBundlePacking(),
                new GenerateBundleCommands(),
                new WriteSerializedFiles(),
                new ArchiveAndCompressBundles()
            };

            var buildParams = new BundleBuildParameters(builderInput.Target, builderInput.TargetGroup, Path.GetDirectoryName(filepath));
            if (builderInput.Target == BuildTarget.WebGL)
                buildParams.BundleCompression = BuildCompression.LZ4Runtime;
            var retCode = ContentPipeline.BuildAssetBundles(buildParams, bundleBuildContent, out IBundleBuildResults result, buildTasks);

            if (Directory.Exists(tempFolderPath))
            {
                Directory.Delete(tempFolderPath, true);
                builderInput.Registry.RemoveFile(tempFilePath);
            }

            var tempFolderMetaFile = tempFolderPath + ".meta";
            if (File.Exists(tempFolderMetaFile))
            {
                File.Delete(tempFolderMetaFile);
                builderInput.Registry.RemoveFile(tempFolderMetaFile);
            }

            if (File.Exists(filepath))
            {
                builderInput.Registry.AddFile(filepath);
            }

            return retCode;
        }
#endif

        static string[] CreateRemoteCatalog(
#if ENABLE_JSON_CATALOG
            string jsonText
#else
            byte[] data
#endif
            , List<ResourceLocationData> locations, AddressableAssetSettings aaSettings, AddressablesDataBuilderInput builderInput,
            ProviderLoadRequestOptions catalogLoadOptions, string contentHash, CatalogBuildInfo catalogBuildInfo)
        {
            string[] dependencyHashes = null;
            string fileName = catalogBuildInfo.fileName;
            string identifier = catalogBuildInfo.identifier;

            if (string.IsNullOrEmpty(contentHash))
            {
#if ENABLE_JSON_CATALOG
                contentHash = HashingMethods.Calculate(jsonText).ToString();
#else
                contentHash = HashingMethods.Calculate(data).ToString();
#endif
            }

            var versionedFileName = aaSettings.profileSettings.EvaluateString(aaSettings.activeProfileId,
                $"/{fileName}_" + builderInput.PlayerVersion);

            string remoteBuildFolder;
            string remoteLoadFolder;

            if (catalogBuildInfo.IsDefaultCatalog)
            {
                remoteBuildFolder = aaSettings.RemoteCatalogBuildPath.GetValue(aaSettings);
                remoteLoadFolder = aaSettings.RemoteCatalogLoadPath.GetValue(aaSettings);
                remoteLoadFolder = remoteLoadFolder.Remove(remoteLoadFolder.Length - 1);
            }
            else
            {
                remoteBuildFolder = catalogBuildInfo.buildPath;
                remoteLoadFolder = catalogBuildInfo.loadPath;
            }

            if (string.IsNullOrEmpty(remoteBuildFolder) ||
                string.IsNullOrEmpty(remoteLoadFolder) ||
                remoteBuildFolder == AddressableAssetProfileSettings.undefinedEntryValue ||
                remoteLoadFolder == AddressableAssetProfileSettings.undefinedEntryValue)
            {
                Addressables.LogWarning(
                    "Remote Build and/or Load paths are not set on the main AddressableAssetSettings asset, but 'Build Remote Catalog' is true.  Cannot create remote catalog.  In the inspector for any group, double click the 'Addressable Asset Settings' object to begin inspecting it. '" +
                    remoteBuildFolder + "', '" + remoteLoadFolder + "'");
            }
            else
            {

                var remoteJsonBuildPath = remoteBuildFolder + Utility.AppendFileNameExtension(versionedFileName);
                var remoteHashBuildPath = remoteBuildFolder + versionedFileName + ".hash";

#if ENABLE_JSON_CATALOG
                WriteFile(remoteJsonBuildPath, jsonText, builderInput.Registry);
#else
                WriteFile(remoteJsonBuildPath, data, builderInput.Registry);
#endif

                WriteFile(remoteHashBuildPath, contentHash, builderInput.Registry);

                dependencyHashes = new string[(int)ContentCatalogProvider.DependencyHashIndex.Count];
                dependencyHashes[(int)ContentCatalogProvider.DependencyHashIndex.Remote] = identifier + "RemoteHash";
                dependencyHashes[(int)ContentCatalogProvider.DependencyHashIndex.Cache] = identifier + "CacheHash";
                dependencyHashes[(int)ContentCatalogProvider.DependencyHashIndex.Local] = identifier + "LocalHash";

                var remoteHashLoadPath = remoteLoadFolder + versionedFileName + ".hash";
                var remoteHashLoadLocation = new ResourceLocationData(
                    new[] { dependencyHashes[(int)ContentCatalogProvider.DependencyHashIndex.Remote] },
                    remoteHashLoadPath,
                    typeof(TextDataProvider), typeof(string))
                {
                    Data = catalogLoadOptions.Copy()
                };

                if (catalogBuildInfo.registerToSettings) locations?.Add(remoteHashLoadLocation);

#if UNITY_SWITCH
                var cacheLoadPath = remoteHashLoadPath; // ResourceLocationBase does not allow empty string id
#else
                var cacheLoadPath = "{UnityEngine.Application.persistentDataPath}/com.unity.addressables" + versionedFileName + ".hash";
#endif
                var cacheLoadLocation = new ResourceLocationData(
                    new[] { dependencyHashes[(int)ContentCatalogProvider.DependencyHashIndex.Cache] },
                    cacheLoadPath,
                    typeof(TextDataProvider), typeof(string))
                {
                    Data = catalogLoadOptions.Copy()
                };
                if (catalogBuildInfo.registerToSettings) locations?.Add(cacheLoadLocation);

                var localCatalogLoadPath = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/" + $"{fileName.Split('.').First()}.hash";
                var localLoadLocation = new ResourceLocationData(
                    new[] { dependencyHashes[(int)ContentCatalogProvider.DependencyHashIndex.Local] },
                    localCatalogLoadPath,
                    typeof(TextDataProvider), typeof(string));
                if (catalogBuildInfo.registerToSettings) locations?.Add(localLoadLocation);
            }

            return dependencyHashes;
        }

        private static string GetProjectName() { return new DirectoryInfo(Path.GetDirectoryName(Application.dataPath) ?? string.Empty).Name; }

        private static void SetAssetEntriesBundleFileIdToCatalogEntryBundleFileId(ICollection<AddressableAssetEntry> assetEntries, Dictionary<string, string> bundleNameToInternalBundleIdMap,
            IBundleWriteData writeData, Dictionary<string, ContentCatalogDataEntry> locationIdToCatalogEntryMap)
        {
            foreach (var loc in assetEntries)
            {
                AddressableAssetEntry processedEntry = loc;
                if (loc.IsFolder && loc.SubAssets.Count > 0)
                    processedEntry = loc.SubAssets[0];
                GUID guid = new GUID(processedEntry.guid);
                //For every entry in the write data we need to ensure the BundleFileId is set so we can save it correctly in the cached state
                if (writeData.AssetToFiles.TryGetValue(guid, out List<string> files))
                {
                    string file = files[0];
                    string fullBundleName = writeData.FileToBundle[file];

                    if (!bundleNameToInternalBundleIdMap.TryGetValue(fullBundleName, out var convertedLocation))
                    {
                        Debug.LogException(new Exception($"Unable to find bundleId for key: {fullBundleName}."));
                    }

                    if (convertedLocation != null && locationIdToCatalogEntryMap.TryGetValue(convertedLocation,
                            out ContentCatalogDataEntry catalogEntry))
                    {
                        loc.BundleFileId = catalogEntry.InternalId;

                        //This is where we strip out the temporary hash added to the bundle name for Content Update for the AssetEntry
                        if (loc.parentGroup?.GetSchema<BundledAssetGroupSchema>()?.BundleNaming ==
                            BundledAssetGroupSchema.BundleNamingStyle.NoHash)
                        {
                            loc.BundleFileId = StripHashFromBundleLocation(loc.BundleFileId);
                        }
                    }
                }
            }
        }

        static string StripHashFromBundleLocation(string hashedBundleLocation)
        {
            return hashedBundleLocation.Remove(hashedBundleLocation.LastIndexOf('_')) + ".bundle";
        }

        /// <inheritdoc />
        protected override string ProcessGroup(AddressableAssetGroup assetGroup, AddressableAssetsBuildContext aaContext)
        {
            if (assetGroup == null)
                return string.Empty;

            if (assetGroup.Schemas.Count == 0)
            {
                Addressables.LogWarning($"{assetGroup.Name} does not have any associated AddressableAssetGroupSchemas. " +
                    $"Data from this group will not be included in the build. " +
                    $"If this is unexpected the AddressableGroup may have become corrupted.");
                return string.Empty;
            }

            foreach (var schema in assetGroup.Schemas)
            {
                var errorString = ProcessGroupSchema(schema, assetGroup, aaContext);
                if (!string.IsNullOrEmpty(errorString))
                    return errorString;
            }

            return string.Empty;
        }

        /// <summary>
        /// Called per group per schema to evaluate that schema.  This can be an easy entry point for implementing the
        ///  build aspects surrounding a custom schema.  Note, you should not rely on schemas getting called in a specific
        ///  order.
        /// </summary>
        /// <param name="schema">The schema to process</param>
        /// <param name="assetGroup">The group this schema was pulled from</param>
        /// <param name="aaContext">The general Addressables build builderInput</param>
        /// <returns></returns>
        private string ProcessGroupSchema(AddressableAssetGroupSchema schema, AddressableAssetGroup assetGroup, AddressableAssetsBuildContext aaContext)
        {
            var bundledAssetSchema = schema as BundledAssetGroupSchema;
            if (bundledAssetSchema != null)
                return ProcessBundledAssetSchema(bundledAssetSchema, assetGroup, aaContext);
            return string.Empty;
        }

        /// <summary>
        /// The processing of the bundled asset schema.  This is where the bundle(s) for a given group are actually setup.
        /// </summary>
        /// <param name="schema">The BundledAssetGroupSchema to process</param>
        /// <param name="assetGroup">The group this schema was pulled from</param>
        /// <param name="aaContext">The general Addressables build builderInput</param>
        /// <returns>The error string, if any.</returns>
        private string ProcessBundledAssetSchema(
            BundledAssetGroupSchema schema,
            AddressableAssetGroup assetGroup,
            AddressableAssetsBuildContext aaContext)
        {
            if (schema == null || !schema.IncludeInBuild || !assetGroup.entries.Any())
                return string.Empty;

            includedGroupsInBuild?.Add(assetGroup);

            AddBundleProvider(schema);

            var assetProviderId = schema.GetAssetCachedProviderId();
            if (createdProviderIds.Add(assetProviderId))
            {
                var assetProviderType = schema.BundledAssetProviderType.Value;
                var assetProviderData = ObjectInitializationData.CreateSerializedInitializationData(assetProviderType, assetProviderId);
                resourceProviderData.Add(assetProviderData);
            }

            string buildPath = schema.BuildPath.GetValue(aaContext.Settings);
            if (buildPath == AddressableAssetProfileSettings.undefinedEntryValue)
                return ($"Addressable group {assetGroup.Name} build path is set to undefined. Change the path to build content.");

            string loadPath = schema.LoadPath.GetValue(aaContext.Settings);
            if (loadPath == AddressableAssetProfileSettings.undefinedEntryValue)
                Addressables.LogWarning($"Addressable group {assetGroup.Name} load path is set to undefined. Change the path to load content.");

            if (loadPath.StartsWith("http://", StringComparison.Ordinal) && PlayerSettings.insecureHttpOption == InsecureHttpOption.NotAllowed)
                Addressables.LogWarning($"Addressable group {assetGroup.Name} uses insecure http for its load path.  To allow http connections for UnityWebRequests, change your settings in Edit > Project Settings > Player > Other Settings > Configuration > Allow downloads over HTTP.");

            if (schema.Compression == BundledAssetGroupSchema.BundleCompressionMode.LZMA && aaContext.runtimeData.BuildTarget == BuildTarget.WebGL.ToString())
                Addressables.LogWarning($"Addressable group {assetGroup.Name} uses LZMA compression, which cannot be decompressed on WebGL. Use LZ4 compression instead.");

            var bundleInputDefs = new List<AssetBundleBuild>();
            var list = PrepGroupBundlePacking(assetGroup, bundleInputDefs, schema);
            aaContext.assetEntries.AddRange(list);
            List<string> uniqueNames = HandleBundleNames(bundleInputDefs, aaContext.bundleToAssetGroup, assetGroup.Guid);
            (string, string)[] groupBundles = new(string, string)[uniqueNames.Count];
            for (int i = 0; i < uniqueNames.Count; ++i)
                groupBundles[i] = (bundleInputDefs[i].assetBundleName, uniqueNames[i]);
            groupToBundleNames.Add(assetGroup, groupBundles);
            allBundleInputDefs.AddRange(bundleInputDefs);
            return string.Empty;
        }

        private static List<string> HandleBundleNames(List<AssetBundleBuild> bundleInputDefs, Dictionary<string, string> bundleToAssetGroup = null, string assetGroupGuid = null)
        {
            var generatedUniqueNames = new List<string>();
            var handledNames = new HashSet<string>();

            for (int i = 0; i < bundleInputDefs.Count; i++)
            {
                AssetBundleBuild bundleBuild = bundleInputDefs[i];
                string assetBundleName = bundleBuild.assetBundleName;
                if (handledNames.Contains(assetBundleName))
                {
                    int count = 1;
                    var newName = assetBundleName;
                    while (handledNames.Contains(newName) && count < 1000)
                        newName = assetBundleName.Replace(".bundle", $"{count++}.bundle");
                    assetBundleName = newName;
                }

                string hashedAssetBundleName = HashingMethods.Calculate(assetBundleName) + ".bundle";
                generatedUniqueNames.Add(assetBundleName);
                handledNames.Add(assetBundleName);

                bundleBuild.assetBundleName = hashedAssetBundleName;
                bundleInputDefs[i] = bundleBuild;

                if (bundleToAssetGroup != null)
                    bundleToAssetGroup.Add(hashedAssetBundleName, assetGroupGuid);
            }

            return generatedUniqueNames;
        }

        private static string CalculateGroupHash(BundledAssetGroupSchema.BundleInternalIdMode mode, AddressableAssetGroup assetGroup, IEnumerable<AddressableAssetEntry> entries)
        {
            switch (mode)
            {
                case BundledAssetGroupSchema.BundleInternalIdMode.GroupGuid: return assetGroup.Guid;
                case BundledAssetGroupSchema.BundleInternalIdMode.GroupGuidProjectIdHash: return HashingMethods.Calculate(assetGroup.Guid, Application.cloudProjectId).ToString();
                case BundledAssetGroupSchema.BundleInternalIdMode.GroupGuidProjectIdEntriesHash:
                    return HashingMethods.Calculate(assetGroup.Guid, Application.cloudProjectId, new HashSet<string>(entries.Select(e => e.guid))).ToString();
            }

            throw new Exception("Invalid naming mode.");
        }

        /// <summary>
        /// Processes an AddressableAssetGroup and generates AssetBundle input definitions based on the BundlePackingMode.
        /// </summary>
        /// <param name="assetGroup">The AddressableAssetGroup to be processed.</param>
        /// <param name="bundleInputDefs">The list of bundle definitions fed into the build pipeline AssetBundleBuild</param>
        /// <param name="schema">The BundledAssetGroupSchema of used to process the assetGroup.</param>
        /// <param name="entryFilter">A filter to remove AddressableAssetEntries from being processed in the build.</param>
        /// <returns>The total list of AddressableAssetEntries that were processed.</returns>
        private static List<AddressableAssetEntry> PrepGroupBundlePacking(AddressableAssetGroup assetGroup, List<AssetBundleBuild> bundleInputDefs, BundledAssetGroupSchema schema,
            Func<AddressableAssetEntry, bool> entryFilter = null)
        {
            var combinedEntries = new List<AddressableAssetEntry>();
            var packingMode = schema.BundleMode;
            var namingMode = schema.InternalBundleIdMode;
            bool ignoreUnsupportedFilesInBuild = assetGroup.Settings.IgnoreUnsupportedFilesInBuild;

            switch (packingMode)
            {
                case BundledAssetGroupSchema.BundlePackingMode.PackTogether:
                {
                    var allEntries = new List<AddressableAssetEntry>();
                    foreach (AddressableAssetEntry a in assetGroup.entries)
                    {
                        if (entryFilter != null && !entryFilter(a))
                            continue;
                        a.GatherAllAssets(allEntries, true, true, false, entryFilter);
                    }

                    combinedEntries.AddRange(allEntries);
                    GenerateBuildInputDefinitions(allEntries, bundleInputDefs, CalculateGroupHash(namingMode, assetGroup, allEntries), "all", ignoreUnsupportedFilesInBuild);
                }
                break;
                case BundledAssetGroupSchema.BundlePackingMode.PackSeparately:
                {
                    foreach (AddressableAssetEntry a in assetGroup.entries)
                    {
                        if (entryFilter != null && !entryFilter(a))
                            continue;
                        var allEntries = new List<AddressableAssetEntry>();
                        a.GatherAllAssets(allEntries, true, true, false, entryFilter);
                        combinedEntries.AddRange(allEntries);
                        GenerateBuildInputDefinitions(allEntries, bundleInputDefs, CalculateGroupHash(namingMode, assetGroup, allEntries), a.address, ignoreUnsupportedFilesInBuild);
                    }
                }
                break;
                case BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel:
                {
                    var labelTable = new Dictionary<string, List<AddressableAssetEntry>>();
                    foreach (AddressableAssetEntry a in assetGroup.entries)
                    {
                        if (entryFilter != null && !entryFilter(a))
                            continue;
                        var sb = new StringBuilder();
                        foreach (var l in a.labels)
                            sb.Append(l);
                        var key = sb.ToString();
                        List<AddressableAssetEntry> entries;
                        if (!labelTable.TryGetValue(key, out entries))
                            labelTable.Add(key, entries = new List<AddressableAssetEntry>());
                        entries.Add(a);
                    }

                    foreach (var entryGroup in labelTable)
                    {
                        var allEntries = new List<AddressableAssetEntry>();
                        foreach (var a in entryGroup.Value)
                        {
                            if (entryFilter != null && !entryFilter(a))
                                continue;
                            a.GatherAllAssets(allEntries, true, true, false, entryFilter);
                        }

                        combinedEntries.AddRange(allEntries);
                        GenerateBuildInputDefinitions(allEntries, bundleInputDefs, CalculateGroupHash(namingMode, assetGroup, allEntries), entryGroup.Key, ignoreUnsupportedFilesInBuild);
                    }
                }
                break;
                default:
                    throw new Exception("Unknown Packing Mode");
            }

            return combinedEntries;
        }

        private static void GenerateBuildInputDefinitions(List<AddressableAssetEntry> allEntries, List<AssetBundleBuild> buildInputDefs, string groupGuid, string address,
            bool ignoreUnsupportedFilesInBuild)
        {
            var scenes = new List<AddressableAssetEntry>();
            var assets = new List<AddressableAssetEntry>();
            foreach (var e in allEntries)
            {
                ThrowExceptionIfInvalidFiletypeOrAddress(e, ignoreUnsupportedFilesInBuild);
                if (string.IsNullOrEmpty(e.AssetPath))
                    continue;
                if (e.IsScene)
                    scenes.Add(e);
                else
                    assets.Add(e);
            }

            if (assets.Count > 0)
                buildInputDefs.Add(GenerateBuildInputDefinition(assets, groupGuid + "_assets_" + address + ".bundle"));
            if (scenes.Count > 0)
                buildInputDefs.Add(GenerateBuildInputDefinition(scenes, groupGuid + "_scenes_" + address + ".bundle"));
        }

        private static void ThrowExceptionIfInvalidFiletypeOrAddress(AddressableAssetEntry entry, bool ignoreUnsupportedFilesInBuild)
        {
            if (entry.guid.Length > 0 && entry.address.Contains('[') && entry.address.Contains(']'))
                throw new Exception($"Address '{entry.address}' cannot contain '[ ]'.");
            if (entry.MainAssetType == typeof(DefaultAsset) && !AssetDatabase.IsValidFolder(entry.AssetPath))
            {
                if (ignoreUnsupportedFilesInBuild)
                    Debug.LogWarning($"Cannot recognize file type for entry located at '{entry.AssetPath}'. Asset location will be ignored.");
                else
                    throw new Exception($"Cannot recognize file type for entry located at '{entry.AssetPath}'. Asset import failed for using an unsupported file type.");
            }
        }

        internal static AssetBundleBuild GenerateBuildInputDefinition(List<AddressableAssetEntry> assets, string name)
        {
            var assetInternalIds = new HashSet<string>();
            var assetsInputDef = new AssetBundleBuild();
            assetsInputDef.assetBundleName = name.ToLower().Replace(" ", "").Replace('\\', '/').Replace("//", "/");
            assetsInputDef.assetNames = assets.Select(s => s.AssetPath).ToArray();
            assetsInputDef.addressableNames = assets.Select(s => s.GetAssetLoadPath(true, assetInternalIds)).ToArray();
            return assetsInputDef;
        }



        static IList<IBuildTask> RuntimeDataBuildTasks(string builtinBundleName, string monoScriptBundleName)
        {
            var buildTasks = new List<IBuildTask>();

            // Setup
            buildTasks.Add(new SwitchToBuildPlatform());
            buildTasks.Add(new RebuildSpriteAtlasCache());

            // Player Scripts
            if (!skipCompilePlayerScripts)
                buildTasks.Add(new BuildPlayerScripts());
            buildTasks.Add(new PostScriptsCallback());

            // Dependency
            buildTasks.Add(new CalculateSceneDependencyData());
            buildTasks.Add(new CalculateAssetDependencyData());
            buildTasks.Add(new AddHashToBundleNameTask());
            buildTasks.Add(new StripUnusedSpriteSources());
            buildTasks.Add(new CreateBuiltInBundle(builtinBundleName));
            if (!string.IsNullOrEmpty(monoScriptBundleName))
                buildTasks.Add(new CreateMonoScriptBundle(monoScriptBundleName));
            buildTasks.Add(new PostDependencyCallback());

            // Packing
            buildTasks.Add(new GenerateBundlePacking());
            buildTasks.Add(new UpdateBundleObjectLayout());
            buildTasks.Add(new GenerateBundleCommands());
            buildTasks.Add(new GenerateSubAssetPathMaps());
            buildTasks.Add(new GenerateBundleMaps());
            buildTasks.Add(new PostPackingCallback());

            // Writing
            buildTasks.Add(new WriteSerializedFiles());
            buildTasks.Add(new ArchiveAndCompressBundles());
            buildTasks.Add(new GenerateLocationListsTask());
            buildTasks.Add(new PostWritingCallback());

            return buildTasks;
        }

        static void MoveFileToDestinationWithTimestampIfDifferent(string srcPath, string destPath, IBuildLogger log)
        {
            if (srcPath == destPath)
                return;

            DateTime time = File.GetLastWriteTime(srcPath);
            DateTime destTime = File.Exists(destPath) ? File.GetLastWriteTime(destPath) : new DateTime();

            if (destTime == time)
                return;

            using (log.ScopedStep(LogLevel.Verbose, "Move File", $"{srcPath} -> {destPath}"))
            {
                var directory = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                else if (File.Exists(destPath))
                    File.Delete(destPath);
                File.Move(srcPath, destPath);
            }
        }

        void PostProcessBundles(AddressableAssetGroup assetGroup, IBundleBuildResults buildResult, AddressablesPlayerBuildResult addrResult, FileRegistry registry,
            AddressableAssetsBuildContext aaContext, Dictionary<string, string> bundleRenameMap, List<Action> postCatalogUpdateCallbacks, AddressableAssetGroup sharedBundleGroup)
        {
            var schema = assetGroup.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
                return;

            var path = schema.BuildPath.GetValue(assetGroup.Settings);
            if (string.IsNullOrEmpty(path))
                return;

            List<string> builtBundleNames = aaContext.assetGroupToBundles[assetGroup];
            List<string> outputBundleNames = null;

            if (groupToBundleNames.TryGetValue(assetGroup, out (string, string)[] bundleValues))
            {
                outputBundleNames = new List<string>(builtBundleNames.Count);
                foreach (var bundleName in builtBundleNames)
                {
                    string outputName = null;
                    foreach ((string, string)bundleValue in bundleValues)
                    {
                        if (schema.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackSeparately)
                        {
                            if (bundleName.StartsWith(bundleValue.Item1, StringComparison.Ordinal))
                                outputName = bundleValue.Item2;
                        }
                        else if (bundleName.Equals(bundleValue.Item1, StringComparison.Ordinal))
                            outputName = bundleValue.Item2;

                        if (outputName != null)
                            break;
                    }
                    outputBundleNames.Add(string.IsNullOrEmpty(outputName) ? bundleName : outputName);
                }
            }
            else
            {
                outputBundleNames = new List<string>(builtBundleNames);
            }

            for (int i = 0; i < builtBundleNames.Count; ++i)
            {
                AddressablesPlayerBuildResult.BundleBuildResult bundleResultInfo = new AddressablesPlayerBuildResult.BundleBuildResult();
                bundleResultInfo.SourceAssetGroup = assetGroup;

                if (GetPrimaryKeyToLocation(aaContext.locations).TryGetValue(builtBundleNames[i], out ContentCatalogDataEntry dataEntry))
                {
                    var info = buildResult.BundleInfos[builtBundleNames[i]];
                    bundleResultInfo.Crc = info.Crc;
                    bundleResultInfo.Hash = info.Hash.ToString();
                    var requestOptions = new AssetBundleRequestOptions
                    {
                        Crc = schema.UseAssetBundleCrc ? info.Crc : 0,
                        UseCrcForCachedBundle = schema.UseAssetBundleCrcForCachedBundles,
                        UseUnityWebRequestForLocalBundles = schema.UseUnityWebRequestForLocalBundles,
                        Hash = schema.UseAssetBundleCache ? info.Hash.ToString() : "",
                        ChunkedTransfer = schema.ChunkedTransfer,
                        RedirectLimit = schema.RedirectLimit,
                        RetryCount = schema.RetryCount,
                        Timeout = schema.Timeout,
                        BundleName = Path.GetFileNameWithoutExtension(info.FileName),
                        AssetLoadMode = schema.AssetLoadMode,
                        BundleSize = GetFileSize(info.FileName),
                        ClearOtherCachedVersionsWhenLoaded = schema.AssetBundledCacheClearBehavior == BundledAssetGroupSchema.CacheClearBehavior.ClearWhenWhenNewVersionLoaded
                    };
                    dataEntry.Data = requestOptions;
                    bundleResultInfo.InternalBundleName = requestOptions.BundleName;

                    if (assetGroup == sharedBundleGroup && info.Dependencies.Length == 0 && !string.IsNullOrEmpty(info.FileName) &&
                        (info.FileName.EndsWith($"{BuiltInBundleBaseName}.bundle", StringComparison.Ordinal)
                         || info.FileName.EndsWith("_monoscripts.bundle", StringComparison.Ordinal)))
                    {
                        outputBundleNames[i] = ConstructAssetBundleName(null, schema, info, outputBundleNames[i]);
                    }
                    else
                    {
                        int extensionLength = Path.GetExtension(outputBundleNames[i]).Length;
                        string[] deconstructedBundleName = outputBundleNames[i].Substring(0, outputBundleNames[i].Length - extensionLength).Split('_');
                        string reconstructedBundleName = string.Join("_", deconstructedBundleName, 1, deconstructedBundleName.Length - 1) + ".bundle";
                        outputBundleNames[i] = ConstructAssetBundleName(assetGroup, schema, info, reconstructedBundleName);
                    }

                    dataEntry.InternalId = dataEntry.InternalId.Remove(dataEntry.InternalId.Length - builtBundleNames[i].Length) + outputBundleNames[i];
                    SetPrimaryKey(dataEntry, outputBundleNames[i], aaContext);

                    if (!bundleToInternalId.ContainsKey(builtBundleNames[i]))
                        bundleToInternalId.Add(builtBundleNames[i], dataEntry.InternalId);

                    if (dataEntry.InternalId.StartsWith("http:\\", StringComparison.Ordinal))
                        dataEntry.InternalId = dataEntry.InternalId.Replace("http:\\", "http://").Replace("\\", "/");
                    else if (dataEntry.InternalId.StartsWith("https:\\", StringComparison.Ordinal))
                        dataEntry.InternalId = dataEntry.InternalId.Replace("https:\\", "https://").Replace("\\", "/");
                }
                else
                {
                    Debug.LogWarningFormat("Unable to find ContentCatalogDataEntry for bundle {0}.", outputBundleNames[i]);
                }

                var targetPath = Path.Combine(path, outputBundleNames[i]);
                bundleResultInfo.FilePath = targetPath;
                var srcPath = Path.Combine(assetGroup.Settings.buildSettings.bundleBuildPath, builtBundleNames[i]);

                if (assetGroup.GetSchema<BundledAssetGroupSchema>()?.BundleNaming == BundledAssetGroupSchema.BundleNamingStyle.NoHash)
                    outputBundleNames[i] = StripHashFromBundleLocation(outputBundleNames[i]);

                bundleRenameMap.Add(builtBundleNames[i], outputBundleNames[i]);
                MoveFileToDestinationWithTimestampIfDifferent(srcPath, targetPath, Log);
                AddPostCatalogUpdatesInternal(assetGroup, postCatalogUpdateCallbacks, dataEntry, targetPath, registry);

                if (addrResult != null)
                    addrResult.AssetBundleBuildResults.Add(bundleResultInfo);

                registry.AddFile(targetPath);
            }
        }

        private void AddPostCatalogUpdatesInternal(AddressableAssetGroup assetGroup, List<Action> postCatalogUpdates, ContentCatalogDataEntry dataEntry, string targetBundlePath,
            FileRegistry registry)
        {
            if (assetGroup.GetSchema<BundledAssetGroupSchema>()?.BundleNaming ==
                BundledAssetGroupSchema.BundleNamingStyle.NoHash)
            {
                postCatalogUpdates.Add(() =>
                {
                    //This is where we strip out the temporary hash for the final bundle location and filename
                    string bundlePathWithoutHash = StripHashFromBundleLocation(targetBundlePath);
                    if (File.Exists(targetBundlePath))
                    {
                        if (File.Exists(bundlePathWithoutHash))
                            File.Delete(bundlePathWithoutHash);
                        string destFolder = Path.GetDirectoryName(bundlePathWithoutHash);
                        if (!string.IsNullOrEmpty(destFolder) && !Directory.Exists(destFolder))
                            Directory.CreateDirectory(destFolder);

                        File.Move(targetBundlePath, bundlePathWithoutHash);
                    }

                    if (registry != null)
                    {
                        if (!registry.ReplaceBundleEntry(targetBundlePath, bundlePathWithoutHash))
                            Debug.LogErrorFormat("Unable to find registered file for bundle {0}.", targetBundlePath);
                    }

                    if (dataEntry != null)
                        if (DataEntryDiffersFromBundleFilename(dataEntry, bundlePathWithoutHash))
                            dataEntry.InternalId = StripHashFromBundleLocation(dataEntry.InternalId);
                });
            }
        }

        // if false, there is no need to remove the hash from dataEntry.InternalId
        private bool DataEntryDiffersFromBundleFilename(ContentCatalogDataEntry dataEntry, string bundlePathWithoutHash)
        {
            string dataEntryId = dataEntry.InternalId;
            string dataEntryFilename = Path.GetFileName(dataEntryId);
            string bundleFileName = Path.GetFileName(bundlePathWithoutHash);

            return dataEntryFilename != bundleFileName;
        }

        /// <summary>
        /// Creates a name for an asset bundle using the provided information.
        /// </summary>
        /// <param name="assetGroup">The asset group.</param>
        /// <param name="schema">The schema of the group.</param>
        /// <param name="info">The bundle information.</param>
        /// <param name="assetBundleName">The base name of the asset bundle.</param>
        /// <returns>Returns the asset bundle name with the provided information.</returns>
        private string ConstructAssetBundleName(AddressableAssetGroup assetGroup, BundledAssetGroupSchema schema, BundleDetails info, string assetBundleName)
        {
            if (assetGroup != null)
            {
                string groupName = assetGroup.Name.Replace(" ", "").Replace('\\', '/').Replace("//", "/").ToLower();
                assetBundleName = groupName + "_" + assetBundleName;
            }

            string bundleNameWithHashing = BuildUtility.GetNameWithHashNaming(schema.BundleNaming, info.Hash.ToString(), assetBundleName);
            //For no hash, we need the hash temporarily for content update purposes.  This will be stripped later on.
            if (schema.BundleNaming == BundledAssetGroupSchema.BundleNamingStyle.NoHash)
            {
                bundleNameWithHashing = bundleNameWithHashing.Replace(".bundle", "_" + info.Hash.ToString() + ".bundle");
            }

            return bundleNameWithHashing;
        }

        /// <summary>
        /// Sets the primary key of the given location. Syncing with other locations that have a dependency on this location
        /// </summary>
        /// <param name="forLocation">CatalogEntry to set the primary key for</param>
        /// <param name="newPrimaryKey">New Primary key to set on location</param>
        /// <param name="aaContext">Addressables build context to collect and assign other location data</param>
        /// <exception cref="ArgumentException"></exception>
        private void SetPrimaryKey(ContentCatalogDataEntry forLocation, string newPrimaryKey, AddressableAssetsBuildContext aaContext)
        {
            if (forLocation == null || forLocation.Keys == null || forLocation.Keys.Count == 0)
                throw new ArgumentException("Cannot change primary key. Invalid catalog entry");

            string originalKey = forLocation.Keys[0] as string;
            if (string.IsNullOrEmpty(originalKey))
                throw new ArgumentException("Invalid primary key for catalog entry " + forLocation.ToString());

            forLocation.Keys[0] = newPrimaryKey;
            primaryKeyToLocation.Remove(originalKey);
            primaryKeyToLocation.Add(newPrimaryKey, forLocation);

            if (!GetPrimaryKeyToDependerLocations(aaContext.locations).TryGetValue(originalKey, out var dependers))
                return; // nothing depends on it

            foreach (ContentCatalogDataEntry location in dependers)
            {
                for (int i = 0; i < location.Dependencies.Count; ++i)
                {
                    string keyString = location.Dependencies[i] as string;
                    if (string.IsNullOrEmpty(keyString))
                        continue;
                    if (keyString == originalKey)
                    {
                        location.Dependencies[i] = newPrimaryKey;
                        break;
                    }
                }
            }

            primaryKeyToDependers.Remove(originalKey);
            primaryKeyToDependers.Add(newPrimaryKey, dependers);
        }

        private static long GetFileSize(string fileName)
        {
            try
            {
                return new FileInfo(fileName).Length;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return 0;
            }
        }

        /// <inheritdoc />
        public override void ClearCachedData()
        {
            if (Directory.Exists(Addressables.BuildPath))
            {
                try
                {
#if ENABLE_JSON_CATALOG
                    var catalogPath = Addressables.BuildPath + "/catalog.json";
                    DeleteFile(catalogPath);
#else
                    var catalogPath = Addressables.BuildPath + "/catalog.bin";
                    DeleteFile(catalogPath);
#endif
                    var settingsPath = Addressables.BuildPath + "/settings.json";
                    DeleteFile(settingsPath);
                    Directory.Delete(Addressables.BuildPath, true);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <inheritdoc />
        public override bool IsDataBuilt()
        {
            var settingsPath = Addressables.BuildPath + "/settings.json";
            return !String.IsNullOrEmpty(catalogBuildPath) &&
                File.Exists(catalogBuildPath) &&
                File.Exists(settingsPath);
        }
    }
}
