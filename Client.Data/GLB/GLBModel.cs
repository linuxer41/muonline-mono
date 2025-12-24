namespace Client.Data.GLB
{
    public class GLBModel
    {
        public byte Version { get; set; } = 0x0C;
        public string Name { get; set; } = string.Empty;

        public GLBMesh[] Meshes { get; set; } = [];
        public GLBBone[] Bones { get; set; } = [];
        public GLBAction[] Actions { get; set; } = [];
    }

    public class GLBMesh
    {
        public GLBVertex[] Vertices { get; set; } = [];
        public GLBNormal[] Normals { get; set; } = [];
        public GLBTexCoord[] TexCoords { get; set; } = [];
        public GLBTriangle[] Triangles { get; set; } = [];
        public short Texture { get; set; } = 0;
        public string TexturePath { get; set; } = string.Empty;

        //for custom blending from json
        public string? BlendingMode { get; set; }
    }

    public struct GLBVertex
    {
        public short Node;
        public System.Numerics.Vector3 Position;
    }

    public struct GLBNormal
    {
        public short Node;
        public System.Numerics.Vector3 Normal;
        public short BindVertex;
    }

    public struct GLBTexCoord
    {
        public float U;
        public float V;
    }

    public struct GLBTriangle
    {
        public byte Polygon;
        public short[] VertexIndex;
        public short[] NormalIndex;
        public short[] TexCoordIndex;
        public GLBTexCoord[] LightMapCoord;
        public short LightMapIndexes;
    }

    public class GLBBone
    {
        public string Name { get; set; } = string.Empty;
        public short Parent { get; set; } = 0;
        public GLBBoneMatrix[] Matrixes { get; set; } = [];
    }

    public struct GLBBoneMatrix
    {
        public System.Numerics.Vector3[] Position;
        public System.Numerics.Vector3[] Rotation;
        public System.Numerics.Quaternion[] Quaternion;
    }

    public class GLBAction
    {
        public int NumAnimationKeys { get; set; }
        public bool LockPositions { get; set; }
        public System.Numerics.Vector3[] Positions { get; set; } = [];
        public float PlaySpeed { get; set; } = 1f;
    }
}