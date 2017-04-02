using SharpDX;

namespace _3DEngine
{
    public class Mesh
    {
        public string Name { get; set; }
        public Vertex[] Verts { get; private set; }
        public Vector3 Pos { get; set; }
        public Vector3 Rot { get; set; }
        public Face[] Faces { get; set; }
        public Texture Texture { get; set; }

        public Mesh(string name, int numVerts, int numFaces)
        {
            Faces = new Face[numFaces];
            Verts = new Vertex[numVerts];
            Name = name;
        }
    }
}

public struct Vertex
{
    public Vector3 Normal;
    public Vector3 Coords;
    public Vector3 WorldCoords;
    public Vector2 TextureCoords;
}