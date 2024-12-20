using System;
using Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.Interface;
using Editor.Extenstion.Build.MultiCatalogHash.Tools;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.BuilderInput
{
    [Serializable]
    public class SerializableAddressablesDataBuilderInput : ISerializableBuilderInput<AddressablesDataBuilderInput>
    {
        public string playerVersion;
        public string targetGroup;
        public string target;
        public string pathSuffix;
        public string runtimeSettingsFilename;
        public string runtimeCatalogFilename;
        public bool isBuildAndRelease;
        public bool isContentUpdateBuild;
        public SerializableFileRegistry registry = new SerializableFileRegistry();

        public SerializableAddressablesDataBuilderInput() {}

        public SerializableAddressablesDataBuilderInput(AddressablesDataBuilderInput input)
        {
            FromOriginal(input);
        }

        public void FromOriginal(AddressablesDataBuilderInput input)
        {
            playerVersion = input.PlayerVersion;
            targetGroup = input.TargetGroup.ToString();
            target = input.Target.ToString();
            pathSuffix = input.PathSuffix;
            runtimeSettingsFilename = input.RuntimeSettingsFilename;
            runtimeCatalogFilename = input.RuntimeCatalogFilename;

            registry.FromOriginal(input.Registry);

            // 获取 internal 字段值
            isBuildAndRelease = (bool)ReflectionHelper.GetFieldValue(input, "IsBuildAndRelease");
            isContentUpdateBuild = (bool)ReflectionHelper.GetFieldValue(input, "IsContentUpdateBuild");
        }

        public AddressablesDataBuilderInput ToOriginal(AddressableAssetSettings settings)
        {
            var output = new AddressablesDataBuilderInput(settings)
            {
                PlayerVersion = playerVersion,
                PathSuffix = pathSuffix,
                RuntimeSettingsFilename = runtimeSettingsFilename,
                RuntimeCatalogFilename = runtimeCatalogFilename,
            };

            // 反序列化字符串为枚举类型
            if (Enum.TryParse(targetGroup, out BuildTargetGroup parsedTargetGroup))
                ReflectionHelper.SetFieldValue(output, "TargetGroup", parsedTargetGroup);

            if (Enum.TryParse(target, out BuildTarget parsedTarget))
                ReflectionHelper.SetFieldValue(output, "Target", parsedTarget);

            // 设置 internal 字段值
            ReflectionHelper.SetFieldValue(output, "IsBuildAndRelease", isBuildAndRelease);
            ReflectionHelper.SetFieldValue(output, "IsContentUpdateBuild", isContentUpdateBuild);
            ReflectionHelper.SetFieldValue(output, "Registry", registry.ToOriginal());

            return output;
        }
    }
}