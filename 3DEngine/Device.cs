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
        private readonly float[] zBuffer; //zBuffer to be used in z-buffering
        private object[] lockBuffer; //Used to ensure we don't try to edit the same pixel on the parallel for in Render
        private readonly int rendW; //Used in z-Buffering
        private readonly int rendH; //Used in z-Buffering

        public Device(WriteableBitmap bitMap) //Constructor for our device
        {
            this.bitMap = bitMap; //Sets the bitMap
            rendW = bitMap.PixelWidth;
            rendH = bitMap.PixelHeight;

            bkBuff = new byte[rendW * rendH * 4]; // Creates our back buffer based on size of our bitmap. It is *4 since we must store a byte for R,G,B and A values
            zBuffer = new float[rendW * rendH];

            lockBuffer = new object[rendW * rendH];
            for (var i = 0; i < lockBuffer.Length; i++)
            {
                lockBuffer[i] = new object();
            }
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

            for (var i = 0; i < zBuffer.Length; i++)
            {
                zBuffer[i] = float.MaxValue; //Reset zBuffer
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

        public Vector3 ProjectTo2D(Vector3 point, Matrix transformation)
        {
            var newPoint = Vector3.TransformCoordinate(point, transformation); // Make the transformation in 3D Space

            //2D space drawing on the screen has point (0,0) as top left of the screen, so convert from 3D space where we have centre of screen being (0,0,0) 
            var x = newPoint.X * rendW + rendW / 2.0f;
            var y = -newPoint.Y * rendH + rendH / 2.0f;
            return (new Vector3(x, y, newPoint.Z));
        }

        public void PutPixel(int x, int y, float z, Color4 colour)
        {
            var i = (x + y * rendW) ; //Calculate index in bkpBuff of (x,y)
            lock (lockBuffer[i]) //Ensures we don't edit a pixel with the same i (i.e. position i.e. the same pixel in our Parallel for)
            {
                if (zBuffer[i] < z)
                {
                    return; // Discard
                }
                zBuffer[i] = z;

                i *= 4; //index in bytes

                bkBuff[i + 2] = (byte)(colour.Red * 255);
                bkBuff[i + 1] = (byte)(colour.Green * 255); ;
                bkBuff[i] = (byte)(colour.Blue * 255); ;
                bkBuff[i + 3] = (byte)(colour.Alpha * 255);
            };

        }

        public void DrawPoint(Vector3 point, Color4 color)
        {
            if (point.X >= 0 && point.Y >= 0 && point.X < rendW && point.Y < rendH)
            {
                PutPixel((int)point.X, (int)point.Y, point.Z, color);
            }
        }

        public void Render(Camera camera, params Mesh[] meshes)
        {
            var viewMat = Matrix.LookAtLH(camera.Position, camera.Target, Vector3.UnitY);
            var projMat = Matrix.PerspectiveFovLH(0.78f, //fov
                                               (float)rendW / rendH, //aspect
                                               0.01f, //znear
                                               1.0f); //zfar

            foreach (Mesh curr in meshes)
            {
                var worldMat = Matrix.RotationYawPitchRoll(curr.Rot.Y, //yaw
                                                           curr.Rot.X, //pitch
                                                           curr.Rot.Z) //roll
                                                           * Matrix.Translation(curr.Pos);
                var transformMat = worldMat * viewMat * projMat; //Create world -> projection matrix
                Parallel.For(0, curr.Faces.Length, faceIndex =>
               {
                   var face = curr.Faces[faceIndex];
                   var vertexA = curr.Verts[face.A];
                   var vertexB = curr.Verts[face.B];
                   var vertexC = curr.Verts[face.C];

                   var pixelA = ProjectTo2D(vertexA, transformMat);
                   var pixelB = ProjectTo2D(vertexB, transformMat);
                   var pixelC = ProjectTo2D(vertexC, transformMat);

                   var color = 0.25f + (faceIndex % curr.Faces.Length) * 0.75f / curr.Faces.Length;
                   RasterizeTriangle(pixelA, pixelB, pixelC, new Color4(color, color, color, 1));
                   faceIndex++;
               });
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
                    // Loading the vertex normal exported by Blender
                    var nx = (float)vertArray[vI * vStep + 3].Value;
                    var ny = (float)vertArray[vI * vStep + 4].Value;
                    var nz = (float)vertArray[vI * vStep + 5].Value;
                    mesh.Verts[vI] = new Vertex { Coords = new Vector3(x, y, z), Normal = new Vector3(nx, ny, nz) };
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

        public void RasterizeTriangle(Vector3 pA, Vector3 pB, Vector3 pC, Color4 colour)
        {
            //We want pA to be on top (lowest Y), and pA.y < pB.Y <  pC.Y
            if (pA.Y > pB.Y) //Swap pA and pB if pB is higher than pA
            {
                var temp = pB;
                pB = pA;
                pA = temp;
            }
            //Now pA is definitely higher than pB

            if (pB.Y > pC.Y) //Swap pB and pC if pC is higher than pB
            {
                var temp = pB;
                pB = pC;
                pC = temp;
            }
            //Now pB is definitely higher than pB

            if (pA.Y > pB.Y) //Swap pA and pB if pB is higher than pA
            {
                var temp = pB;
                pB = pA;
                pA = temp;
            }
            //Now pA is definitely the highest, pB the middle and pC the lowest

            float dPAPB, dPAPC;

            if (pB.Y - pA.Y > 0) // i.e. if not zero since pB-pA cannot be negative (pB is lower than pA guaranteed)
            {
                dPAPB = (pB.X - pA.X) / (pB.Y - pA.Y);
            }
            else
            {
                dPAPB = 0;
            }

            if (pC.Y - pA.Y > 0) // i.e. if not zero since pB-pA cannot be negative (pB is lower than pA guaranteed)
            {
                dPAPC = (pC.X - pA.X) / (pC.Y - pA.Y);
            }
            else
            {
                dPAPC = 0;
            }

            if (dPAPB > dPAPC)
            {
                //Handle case where PB is on the left
                for (var y = (int)pA.Y; y <= (int)pC.Y; y++)
                {
                    if (y < pB.Y)
                    {
                        //Draw scan line in first half of triangle
                        DrawScanLine(y, pA, pC, pA, pB, colour);
                    }
                    else
                    {
                        //Draw scan line in second half of triangle
                        DrawScanLine(y, pA, pC, pB, pC, colour);
                    }
                }

            }
            else
            {
                //Handle case where PB is on the right
                for (var y = (int)pA.Y; y <= (int)pC.Y; y++)
                {
                    if (y < pB.Y)
                    {
                        //Draw scan line in first half of triangle
                        DrawScanLine(y, pA, pB, pA, pC, colour);
                    }
                    else
                    {
                        //Draw scan line in second half of triangle
                        DrawScanLine(y, pB, pC, pA, pC, colour);
                    }
                }
            }
        }

        public void DrawScanLine(float currY, Vector3 pLA, Vector3 pLB, Vector3 pRA, Vector3 pRB, Color4 colour)
        {
            //pLA, pLB define line on left. pRA, pRB define line on the right
            int startX, endX;

            var gradL = pLA.Y != pLB.Y ? (currY - pLA.Y) / (pLB.Y - pLA.Y) : 1; //How far down Left line is our y?
            var gradR = pRA.Y != pRB.Y ? (currY - pRA.Y) / (pRB.Y - pRA.Y) : 1; //How far down Right line is our y?

            startX = (int)(pLA.X + ((pLB.X - pLA.X) * (Math.Max(0, Math.Min(gradL, 1)))));
            endX = (int)(pRA.X + ((pRB.X - pRA.X) * (Math.Max(0, Math.Min(gradR, 1)))));

            float z1 = (pLA.Z + ((pLB.Z - pLA.Z) * (Math.Max(0, Math.Min(gradL, 1)))));
            float z2 = (pRA.Z + ((pRB.Z - pRA.Z) * (Math.Max(0, Math.Min(gradR, 1)))));

            for (var currX = startX; currX < endX; currX++)
            {
                float gradZ = (float)((currX - startX) / (float)(endX - startX));
                var currZ = (z1 + ((z2 - z1) * (Math.Max(0, Math.Min(gradZ, 1)))));
                DrawPoint(new Vector3(currX, currY, currZ), colour);
            }
        }

    }

}
