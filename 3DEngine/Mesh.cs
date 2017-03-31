using SharpDX;

namespace _3DEngine
{
    public class Mesh
    {
        public string Name { get; set; }
        public Vector3[] Verts { get; private set; }
        public Vector3 Pos { get; set; }
        public Vector3 Rot { get; set; }

        public Mesh(string name, int numVert)
        {
            Verts = new Vector3[numVert];
            Name = name;
        }
    }
}
