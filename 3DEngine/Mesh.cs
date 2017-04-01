using SharpDX;

namespace _3DEngine
{
    public class Mesh
    {
        public string Name { get; set; }
        public Vector3[] Verts { get; private set; }
        public Vector3 Pos { get; set; }
        public Vector3 Rot { get; set; }
        public Face[] Faces { get; set; }

        public Mesh(string name, int numVerts, int numFaces)
        {
            Faces = new Face[numFaces];
            Verts = new Vector3[numVerts];
            Name = name;
        }
    }
}
