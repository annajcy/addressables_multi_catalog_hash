using System;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core
{
    public class CatalogSetup
    {
        /// <summary>
        /// External catalogc
        /// </summary>
        public readonly ExternalCatalog externalCatalog = null;

        /// <summary>
        /// The catalog build info.
        /// </summary>
        public readonly CatalogBuildInfo catalogBuildInfo = null;

        /// <summary>
        /// Tells whether the catalog is empty.
        /// </summary>
        public bool IsEmpty => catalogBuildInfo.locations.Count == 0;

        public CatalogSetup(ExternalCatalog externalCatalog, AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext)
        {
            this.externalCatalog = externalCatalog;

            var profileSettings = aaContext.Settings.profileSettings;
            var profileId = aaContext.Settings.activeProfileId;
            var catalogFileName = $"{externalCatalog.CatalogName}";

            catalogBuildInfo = new CatalogBuildInfo(externalCatalog.CatalogName, catalogFileName)
            {
                // Set the build path.
                buildPath = externalCatalog.BuildPath.GetValue(profileSettings, profileId) + $"/{catalogFileName}",
                rootBuildPath = externalCatalog.BuildPath.GetValue(profileSettings, profileId)
            };

            if (string.IsNullOrEmpty(catalogBuildInfo.buildPath))
            {
                catalogBuildInfo.buildPath = profileSettings.EvaluateString(profileId, externalCatalog.BuildPath.Id);
                if (string.IsNullOrWhiteSpace(catalogBuildInfo.buildPath))
                    throw new Exception($"The catalog build path for external catalog '{externalCatalog.name}' is empty.");
            }

            // Set the load path.
            catalogBuildInfo.loadPath = externalCatalog.RuntimeLoadPath.GetValue(profileSettings, profileId) + $"{catalogFileName}";
            if (string.IsNullOrEmpty(catalogBuildInfo.loadPath))
                catalogBuildInfo.loadPath = profileSettings.EvaluateString(profileId, externalCatalog.RuntimeLoadPath.Id);

            catalogBuildInfo.registerToSettings = false;
        }
    }
}