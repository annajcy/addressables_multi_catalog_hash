using System.Collections.Generic;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace Script.BuildScript.Editor.MultiCatalogHash.Core
{
		/// <summary>
    	/// Contains information about a catalog to be built.
    	/// </summary>
    	public class CatalogBuildInfo
    	{
    		/// <summary>
    		/// The catalog identifier.
    		///
    		/// Note that "AddressablesMainContentCatalog" is used for the default main catalog.
    		/// </summary>
    		public string identifier;

    		/// <summary>
    		/// The filename of the JSON file to contain the catalog data.
    		///
    		/// Note that the default main catalog is written to "catalog.json"
    		/// </summary>
    		public readonly string fileName;

    		/// <summary>
    		/// The locations, i.e., the addressable assets, contained in this catalog.
    		/// </summary>
    		public readonly List<ContentCatalogDataEntry> locations = new List<ContentCatalogDataEntry>();

		    /// <summary>
		    /// Included bundles when generating result hash
		    /// </summary>
		    public readonly List<string> includedBundles = new List<string>();

    		/// <summary>
    		/// The directory path this catalog is expected to be build.
    		/// </summary>
    		public string buildPath;

    		/// <summary>
    		/// The directory path this catalog is expected to be loaded from.
    		/// </summary>
    		public string loadPath;

		    /// <summary>
		    /// Root Build Path
		    /// </summary>
		    public string rootBuildPath;

    		/// <summary>
    		/// Determines whether the catalog is going to be registered in settings.json.
    		///
    		/// Registered catalogs are automatically loaded on application startup.
    		/// Use "false" for catalogs that are to be loaded dynamicaly.
    		/// </summary>
    		public bool registerToSettings = true;

		    public bool IsDefaultCatalog => string.Equals(identifier, "AddressablesMainContentCatalog");

    		/// <summary>
    		/// Construct an empty catalog build info.
    		/// </summary>
    		/// <param name="identifier">the identifier</param>
    		/// <param name="fileName">the json filename</param>
    		public CatalogBuildInfo(string identifier, string fileName)
    		{
    			this.identifier = identifier;
    			this.fileName = fileName;
    		}
    	}
}