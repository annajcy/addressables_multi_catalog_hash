using UnityEditor.AddressableAssets.Settings;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.Interface
{
    public interface ISerializableBase {}
    public interface ISerializable<T> : ISerializableBase
    {
        public void FromOriginal(T input);
        public T ToOriginal();
    }

    public interface ISerializableBuilderInput<T> : ISerializableBase
    {
        public void FromOriginal(T input);
        public T ToOriginal(AddressableAssetSettings addressableAssetSettings);
    }
}