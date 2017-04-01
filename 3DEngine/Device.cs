using Windows.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using SharpDX;

namespace _3DEngine
{
    public class Device //This class will be the core of our engine
    {
        private byte[] bkBuff; //This is the back buffer. Every cell is mapped to a pixel of the screen and willbe used to update the front buffer
        private WriteableBitmap bitMap; //This is the source we will be using for the front buffer (our XAML image control)

        public Device(WriteableBitmap bitMap) //Constructor for our device
        {
            this.bitMap = bitMap; //Sets the bitMap

            bkBuff = new byte[bitMap.PixelWidth * bitMap.PixelHeight * 4]; // Creates our back buffer based on size of our bitmap. It is *4 since we must store a byte for R,G,B and A values
        }

        public void Clear(byte r, byte g, byte b, byte a) //Clears the back buffer by setting it to a specific colour
        {
            for(int i=0; i < bkBuff.Length; i += 4)
            {
                //Windows uses bgra nor rgba 
                bkBuff[i + 2] = r;
                bkBuff[i + 1] = g;
                bkBuff[i] = b;
                bkBuff[i + 3] = a;
            }
        }

        //Writes back buffer into our front buffer i.e. bitMap
        public void Flush()
        {
            using (var stm = bitMap.PixelBuffer.AsStream())
            {
                stm.Write(bkBuff, 0, bkBuff.Length); //write back buffer into bitmap
            }
            bitMap.Invalidate(); //Redraw bitmap
        }

        public Vector2 ProjectTo2D(Vector3 point, Matrix transformation)
        {
            var newPoint = Vector3.TransformCoordinate(point, transformation); // Make the transformation in 3D Space

            //2D space drawing on the screen has point (0,0) as top left of the screen, so convert from 3D space where we have centre of screen being (0,0,0) 
            var x = newPoint.X * bitMap.PixelWidth + bitMap.PixelWidth / 2.0f;
            var y = -newPoint.Y * bitMap.PixelHeight + bitMap.PixelHeight / 2.0f;
            return new Vector2(x, y);
        }

        public void PutPixel(int x, int y, Color4 colour)
        {
            var i = (x + y * bitMap.PixelWidth) * 4; //Calculate index in bkpBuff of (x,y)

            bkBuff[i + 2] = (byte)(colour.Red * 255);
            bkBuff[i + 1] = (byte)(colour.Green * 255); ;
            bkBuff[i] = (byte)(colour.Blue * 255); ;
            bkBuff[i + 3] = (byte)(colour.Alpha* 255);
        }

        public bool DrawPoint(Vector2 point)
        {
            if (point.X >= 0 && point.Y >= 0 && point.X < bitMap.PixelWidth && point.Y < bitMap.PixelHeight)
            {
                PutPixel((int)point.X, (int)point.Y, new Color4(1.0f, 0.0f, 0.0f, 1.0f)); // Drawing a Blue point by default
            }
            return false; //Defaults to false if outside of screen params
        }

        public void Render(Camera camera, params Mesh[] meshes)
        {
            var viewMat = Matrix.LookAtLH(camera.Position, camera.Target, Vector3.UnitY);
            var projMat = Matrix.PerspectiveFovRH(0.78f, //fov
                                               (float)bitMap.PixelWidth / bitMap.PixelHeight, //aspect
                                               0.01f, //znear
                                               1.0f); //zfar

            foreach (Mesh curr in meshes)
            {
                var worldMat = Matrix.RotationYawPitchRoll(curr.Rot.Y, //yaw
                                                           curr.Rot.X, //pitch
                                                           curr.Rot.Z) //roll
                                                           * Matrix.Translation(curr.Pos);
                var transformMat = worldMat * viewMat * projMat; //Create world -> projection matrix

                foreach (var vertex in curr.Verts)
                {
                    var point = ProjectTo2D(vertex, transformMat);
                    DrawPoint(point);
                }
            }
        }
    }
}
