using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace _3DEngine
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
       // private Image frontBuffer;
        private Device device;
        Mesh mesh = new Mesh("Cube", 8,12);
        Camera camera = new Camera();

        
        // Rendering loop handler
        void CompositionTarget_Rendering(object sender, object e)
        {
            device.Clear(0, 0, 0, 255);

            // rotating slightly the cube during each frame rendered
            mesh.Rot = new Vector3(mesh.Rot.X + 0.01f, mesh.Rot.Y + 0.01f, mesh.Rot.Z);

            // Doing the various matrix operations
            device.Render(camera, mesh);
            // Flushing the back buffer into the front buffer
            device.Flush();
        }


        public MainPage()
        {
            
            this.InitializeComponent();
            // Choose the back buffer resolution here
            WriteableBitmap bitMap = new WriteableBitmap(640, 480);

            device = new Device(bitMap);

            // Our Image XAML control
            frontBuffer.Source = bitMap; //Sets the image's source to bitMap

            //Next lines creates a cube (mesh)
            mesh.Verts[0] = new Vector3(-1, 1, 1);
            mesh.Verts[1] = new Vector3(1, 1, 1);
            mesh.Verts[2] = new Vector3(-1, -1, 1);
            mesh.Verts[3] = new Vector3(1, -1, 1);
            mesh.Verts[4] = new Vector3(-1, 1, -1);
            mesh.Verts[5] = new Vector3(1, 1, -1);
            mesh.Verts[6] = new Vector3(1, -1, -1);
            mesh.Verts[7] = new Vector3(-1, -1, -1);

            mesh.Faces[0] = new Face { A = 0, B = 1, C = 2 };
            mesh.Faces[1] = new Face { A = 1, B = 2, C = 3 };
            mesh.Faces[2] = new Face { A = 1, B = 3, C = 6 };
            mesh.Faces[3] = new Face { A = 1, B = 5, C = 6 };
            mesh.Faces[4] = new Face { A = 0, B = 1, C = 4 };
            mesh.Faces[5] = new Face { A = 1, B = 4, C = 5 };

            mesh.Faces[6] = new Face { A = 2, B = 3, C = 7 };
            mesh.Faces[7] = new Face { A = 3, B = 6, C = 7 };
            mesh.Faces[8] = new Face { A = 0, B = 2, C = 7 };
            mesh.Faces[9] = new Face { A = 0, B = 4, C = 7 };
            mesh.Faces[10] = new Face { A = 4, B = 5, C = 6 };
            mesh.Faces[11] = new Face { A = 4, B = 6, C = 7 };

            camera.Position = new Vector3(0, 0, 10.0f);
            camera.Target = Vector3.Zero;

            // Registering to the XAML rendering loop
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }
    }
}
