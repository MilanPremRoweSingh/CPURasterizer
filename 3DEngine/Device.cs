using Windows.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using SharpDX;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

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
            for (int i = 0; i < bkBuff.Length; i += 4)
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
            bkBuff[i + 3] = (byte)(colour.Alpha * 255);
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

                foreach (var face in curr.Faces)
                {
                    var vertexA = curr.Verts[face.A];
                    var vertexB = curr.Verts[face.B];
                    var vertexC = curr.Verts[face.C];

                    var pixelA = ProjectTo2D(vertexA, transformMat);
                    var pixelB = ProjectTo2D(vertexB, transformMat);
                    var pixelC = ProjectTo2D(vertexC, transformMat);

                    DrawLineBresenham(pixelA, pixelB);
                    DrawLineBresenham(pixelB, pixelC);
                    DrawLineBresenham(pixelC, pixelA);
                }
            }
        }

        public void DrawLineRecursive(Vector2 pointA, Vector2 pointB)
        {
            var distance = (pointB - pointA).Length(); //Distance between points in pixels

            if (distance <= 1) { return; } // If no pixel between the two, don't draw anything

            Vector2 mid = pointA + (pointB - pointA) / 2;
            DrawPoint(mid);
            DrawLineRecursive(pointA, mid); //Recursively draw points
            DrawLineRecursive(mid, pointB);
        }

        public void DrawLineBresenham(Vector2 pointA, Vector2 pointB) //Draw line using Bresenham algorithm
        {
            int xA = (int)pointA.X;
            int yA = (int)pointA.Y;
            int xB = (int)pointB.X;
            int yB = (int)pointB.Y;

            var dX = System.Math.Abs(xB - xA);
            var dY = System.Math.Abs(yB - yA);
            var signX = (xA < xB) ? 1 : -1;
            var signY = (yA < yB) ? 1 : -1;
            var error = dX - dY;

            while (true)
            {
                DrawPoint(new Vector2(xA, yA));
                if ((xA == xB) && (yA == yB)) { break; }
                var deltaError = 2 * error;
                if (deltaError > -dY) { error -= dY; xA += signX; }
                if (deltaError < dX) { error += dX; yA += signY; }
            }
        }

        public async Task<Mesh[]> LoadJSONFileAsync(string fileName) //Async allows await; This will load / parse the JSON file
        {
            var meshes = new List<Mesh>();
            var file = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync(fileName);
            var data = await Windows.Storage.FileIO.ReadTextAsync(file);
            dynamic jsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject(data);

            for (var mI = 0; mI < jsonObject.meshes.Count; mI++)
            {
                var vertArray = jsonObject.meshes[mI].vertices;
                var indexArray = jsonObject.meshes[mI].indices;

                var uvCount = jsonObject.meshes[mI].uvCount.Value;
                var vStep = 1; //Default value

                //uvCount refers to number of texture coords/vertex
                //We're jumping by 6,8,10 
                switch ((int)uvCount)
                {
                    case 0:
                        vStep = 6;
                        break;
                    case 1:
                        vStep = 8;
                        break;
                    case 2:
                        vStep = 10;
                        break;

                }

                var vertCount = vertArray.Count / vStep;
                var facesCount = indexArray.Count / 3;
                var mesh = new Mesh(jsonObject.meshes[mI].name.Value, vertCount, facesCount);

                for (var vI = 0; vI < vertCount; vI++)
                {
                    var x = (float)vertArray[vI * vStep].Value;
                    var y = (float)vertArray[vI * vStep + 1].Value;
                    var z = (float)vertArray[vI * vStep + 2].Value;
                    mesh.Verts[vI] = new Vector3(x, y, z);
                }

                for (var fI = 0; fI < facesCount; fI++)
                {
                    var a = (int)indexArray[fI * 3].Value;
                    var b = (int)indexArray[fI * 3 + 1].Value;
                    var c = (int)indexArray[fI * 3 + 2].Value;
                    mesh.Faces[fI] = new Face { A = a, B = b, C = c };
                }

                var pos = jsonObject.meshes[mI].position;
                mesh.Pos = new Vector3((float)pos[0].Value, (float)pos[1].Value, (float)pos[2].Value);
                meshes.Add(mesh);
            }
            return meshes.ToArray();
        }
    }
}
