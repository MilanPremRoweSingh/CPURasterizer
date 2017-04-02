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

        public Vertex ProjectTo2D(Vertex vert, Matrix transformation, Matrix worldMat)
        {
            var point2D = Vector3.TransformCoordinate(vert.Coords, transformation); // Make the transformation in 3D 
            var point3dWorld = Vector3.TransformCoordinate(vert.Coords, worldMat);
            var normal3dWorld = Vector3.TransformCoordinate(vert.Normal, worldMat);
            //2D space drawing on the screen has point (0,0) as top left of the screen, so convert from 3D space where we have centre of screen being (0,0,0) 
            var x = point2D.X * rendW + rendW / 2.0f;
            var y = -point2D.Y * rendH + rendH / 2.0f;
            return new Vertex
            {
                Coords = new Vector3(x, y, point2D.Z),
                Normal = normal3dWorld,
                WorldCoords = point3dWorld,
                TextureCoords = vert.TextureCoords
            };
        }

        public void PutPixel(int x, int y, float z, Color4 colour)
        {
            var i = (x + y * rendW); //Calculate index in bkpBuff of (x,y)
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

                    var pixelA = ProjectTo2D(vertexA, transformMat, worldMat);
                    var pixelB = ProjectTo2D(vertexB, transformMat, worldMat);
                    var pixelC = ProjectTo2D(vertexC, transformMat, worldMat);

                    var color = 1.0f;
                    RasterizeTriangle(pixelA, pixelB, pixelC, new Color4(color, color, color, 1), curr.Texture);
                    faceIndex++;
                });
            }
        }

        public void RasterizeTriangle(Vertex vA, Vertex vB, Vertex vC, Color4 colour, Texture texture)
        {
            //We want pA to be on top (lowest Y), and pA.y < pB.Y <  pC.Y
            if (vA.Coords.Y > vB.Coords.Y) //Swap vectors if vB is higher
            {
                var temp = vB;
                vB = vA;
                vA = temp;
            }
            //Now vA is definitely higher than vB

            if (vB.Coords.Y > vC.Coords.Y) //Swap vectors if vC is higher
            {
                var temp = vB;
                vB = vC;
                vC = temp;
            }
            //Now vB is definitely higher than vB

            if (vA.Coords.Y > vB.Coords.Y) //Swap vectors if vB is higher
            {
                var temp = vB;
                vB = vA;
                vA = temp;
            }
            //Now vA is definitely the highest, vB the middle and vC the lowest

            Vector3 pA = vA.Coords;
            Vector3 pB = vB.Coords;
            Vector3 pC = vC.Coords;

            //Compute face normal
            //Vector3 vNormFace = ((vA.Normal + vB.Normal + vC.Normal) / 3); //Face normal is average of 3 vertex normals
            //Vector3 faceCentre = ((vA.WorldCoords + vB.WorldCoords + vC.WorldCoords) / 3); //Middle point of face is avg of 3 vertex word pos

            Vector3 lightPos = new Vector3(0, -10, 0); //Light position
            //Compute the cosing of angles between vertex normals and light
            float vnlA = ComputeNDotL(vA.WorldCoords, vA.Normal, lightPos);
            float vnlB = ComputeNDotL(vB.WorldCoords, vB.Normal, lightPos);
            float vnlC = ComputeNDotL(vC.WorldCoords, vC.Normal, lightPos);
            var data = new ScanLineData { };

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
                    data.currentY = y;
                    if (y < pB.Y)
                    {
                        data.ndotla = vnlA;
                        data.ndotlb = vnlC;
                        data.ndotlc = vnlA;
                        data.ndotld = vnlB;


                        data.ua = vA.TextureCoords.X;
                        data.ub = vC.TextureCoords.X;
                        data.uc = vA.TextureCoords.X;
                        data.ud = vB.TextureCoords.X;

                        data.va = vA.TextureCoords.Y;
                        data.vb = vC.TextureCoords.Y;
                        data.vc = vA.TextureCoords.Y;
                        data.vd = vB.TextureCoords.Y;
                        //Draw scan line in first half of triangle
                        DrawScanLine(vA, vC, vA, vB, colour, data, texture);
                    }
                    else
                    {
                        data.ndotla = vnlA;
                        data.ndotlb = vnlC;
                        data.ndotlc = vnlB;
                        data.ndotld = vnlC;


                        data.ua = vA.TextureCoords.X;
                        data.ub = vC.TextureCoords.X;
                        data.uc = vB.TextureCoords.X;
                        data.ud = vC.TextureCoords.X;

                        data.va = vA.TextureCoords.Y;
                        data.vb = vC.TextureCoords.Y;
                        data.vc = vB.TextureCoords.Y;
                        data.vd = vC.TextureCoords.Y;
                        //Draw scan line in second half of triangle
                        DrawScanLine(vA, vC, vB, vC, colour, data, texture);
                    }
                }

            }
            else
            {
                //Handle case where PB is on the right
                for (var y = (int)pA.Y; y <= (int)pC.Y; y++)
                {
                    data.currentY = y;
                    if (y < pB.Y)
                    {
                        data.ndotla = vnlA;
                        data.ndotlb = vnlB;
                        data.ndotlc = vnlA;
                        data.ndotld = vnlC;

                        data.ua = vA.TextureCoords.X;
                        data.ub = vB.TextureCoords.X;
                        data.uc = vA.TextureCoords.X;
                        data.ud = vC.TextureCoords.X;

                        data.va = vA.TextureCoords.Y;
                        data.vb = vB.TextureCoords.Y;
                        data.vc = vA.TextureCoords.Y;
                        data.vd = vC.TextureCoords.Y;
                        //Draw scan line in first half of triangle
                        DrawScanLine(vA, vB, vA, vC, colour, data, texture);
                    }
                    else
                    {
                        data.ndotla = vnlB;
                        data.ndotlb = vnlC;
                        data.ndotlc = vnlA;
                        data.ndotld = vnlC;

                        data.ua = vB.TextureCoords.X;
                        data.ub = vC.TextureCoords.X;
                        data.uc = vA.TextureCoords.X;
                        data.ud = vC.TextureCoords.X;

                        data.va = vB.TextureCoords.Y;
                        data.vb = vC.TextureCoords.Y;
                        data.vc = vA.TextureCoords.Y;
                        data.vd = vC.TextureCoords.Y;
                        //Draw scan line in second half of triangle
                        DrawScanLine(vB, vC, vA, vC, colour, data, texture);
                    }
                }
            }
        }

        public void DrawScanLine(Vertex vLA, Vertex vLB, Vertex vRA, Vertex vRB, Color4 colour, ScanLineData data, Texture texture)
        {
            Vector3 pLA = vLA.Coords;
            Vector3 pLB = vLB.Coords;
            Vector3 pRA = vRA.Coords;
            Vector3 pRB = vRB.Coords;
            //pLA, pLB define line on left. pRA, pRB define line on the right
            int startX, endX;

            var gradL = pLA.Y != pLB.Y ? (data.currentY - pLA.Y) / (pLB.Y - pLA.Y) : 1; //How far down Left line is our y?
            var gradR = pRA.Y != pRB.Y ? (data.currentY - pRA.Y) / (pRB.Y - pRA.Y) : 1; //How far down Right line is our y?

            //Interpolate Xs
            startX = (int)Interpolate(pLA.X, pLB.X, gradL);
            endX = (int)Interpolate(pRA.X, pRB.X, gradR);

            //Interpolate Zs
            float z1 = Interpolate(pLA.Z, pLB.Z, gradL);
            float z2 = Interpolate(pRA.Z, pRB.Z, gradR);

            //Interpolate normals
            var startLN = Interpolate(data.ndotla, data.ndotlb, gradL);
            var endLN = Interpolate(data.ndotlc, data.ndotld, gradR);

            //Interpolate starting and ending U,V 
            var startU = Interpolate(data.ua, data.ub, gradL);
            var endU = Interpolate(data.uc, data.ud, gradR);
            var startV = Interpolate(data.va, data.vb, gradL);
            var endV = Interpolate(data.vc, data.vd, gradR);

            for (var currX = startX; currX < endX; currX++)
            {
                float grad = (float)((currX - startX) / (float)(endX - startX));
                var currZ = Interpolate(z1, z2, grad);

                var ndotl = Interpolate(startLN, endLN, grad);

                var u = Interpolate(startU, endU, grad);
                var v = Interpolate(startV, endV, grad);

                Color4 textureColor;

                if (texture != null)
                    textureColor = texture.Map(u, v);
                else
                    textureColor = new Color4(1.0f, 1.0f, 1.0f, 1);

                DrawPoint(new Vector3(currX, data.currentY, currZ), (new Color4(colour.Red*textureColor.Red * ndotl,
                                                                                colour.Green * textureColor.Green * ndotl,
                                                                                colour.Blue * textureColor.Blue * ndotl,
                                                                                1)));
            }
        }

        float Interpolate(float min, float max, float gradient)
        {
            return min + (max - min) * (Math.Max(0, Math.Min(gradient, 1)));
        }

        public float ComputeNDotL(Vector3 vert, Vector3 norm, Vector3 lightPos)
        {
            var lightDir = vert - lightPos; //direction from light to vertex

            norm.Normalize(); //Normalise normal
            lightDir.Normalize(); //Normalise lightDir
            var dot = Vector3.Dot(norm, lightDir);
            return Math.Max(0, dot); //Compute Dot and return
        }

        public async Task<Mesh[]> LoadJSONFileAsync(string fileName) //Async allows await; This will load / parse the JSON file
        {
            var meshes = new List<Mesh>();
            var materials = new Dictionary<String, Material>();
            var file = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync(fileName);
            var data = await Windows.Storage.FileIO.ReadTextAsync(file);
            dynamic jsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject(data);

            for (var materialIndex = 0; materialIndex < jsonObject.materials.Count; materialIndex++)
            {
                var material = new Material();
                material.Name = jsonObject.materials[materialIndex].name.Value;
                material.ID = jsonObject.materials[materialIndex].id.Value;
                if (jsonObject.materials[materialIndex].diffuseTexture != null)
                    material.DiffuseTextureName = jsonObject.materials[materialIndex].diffuseTexture.name.Value;

                materials.Add(material.ID, material);
            }


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

                    if (uvCount > 0)
                    {
                        // Loading the texture coordinates
                        float u = (float)vertArray[vI * vStep + 6].Value;
                        float v = (float)vertArray[vI * vStep + 7].Value;
                        mesh.Verts[vI].TextureCoords = new Vector2(u, v);
                    }
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


                if (uvCount > 0)
                {
                    // Texture
                    var meshTextureID = jsonObject.meshes[mI].materialId.Value;
                    var meshTextureName = materials[meshTextureID].DiffuseTextureName;
                    mesh.Texture = new Texture(meshTextureName, 512, 512);
                }
                meshes.Add(mesh);
            }
            return meshes.ToArray();
        }

    }


}
