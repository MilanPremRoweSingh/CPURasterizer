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
        Mesh[] meshes;
        Camera camera = new Camera();

        private async void Load()
        {
            // Choose the back buffer resolution here
            WriteableBitmap bmp = new WriteableBitmap(640, 480);

            // Our Image XAML control
            frontBuffer.Source = bmp;

            device = new Device(bmp);
            meshes = await device.LoadJSONFileAsync("Suzanne.babylon");
            camera.Position = new Vector3(0, 0, 10.0f);
            camera.Target = Vector3.Zero;

            // Registering to the XAML rendering loop
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        // Rendering loop handler
        DateTime previousDate;
        void CompositionTarget_Rendering(object sender, object e)
        { 
            var now = DateTime.Now;
            var currentFps = 1000.0 / (now - previousDate).TotalMilliseconds;
            previousDate = now;

            fps.Text = string.Format("{0:0.00} fps", currentFps);

            device.Clear(0, 0, 0, 255);

            foreach (var mesh in meshes)
            {
                // rotating slightly the cube during each frame rendered
                mesh.Rot = new Vector3(mesh.Rot.X, mesh.Rot.Y + 0.01f, mesh.Rot.Z);
            }
            // Doing the various matrix operations
            device.Render(camera, meshes);
            // Flushing the back buffer into the front buffer
            device.Flush();
        }


        public MainPage()
        {
            
            this.InitializeComponent();
            Load(); //Loads the image into the bitmap dispaly linked to the image in the application
        }
    }
}
