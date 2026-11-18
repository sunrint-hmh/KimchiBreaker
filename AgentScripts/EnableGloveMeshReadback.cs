using UnityEditor;
public static class EnableGloveMeshReadback
{
    public static object Main()
    {
        foreach(string side in new[]{"Left","Right"})
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath("Assets/3d_models/RubberGloves/RubberGlove_"+side+".fbx");
            importer.isReadable=true;importer.SaveAndReimport();
        }
        return new{readWriteEnabled=true,prefabRebuilt=false,sceneSaved=false};
    }
}
