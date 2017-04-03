#3D Engine

This project is a basic 3D engine coded in C# for Windows 10, which displays models imported from blender using Babylon.js (JSON). All calculations are done with the CPU, without the use of extra libraries, with the exception of SharpDX which was used for a the Matrix, Vector and Color data structures. I decided to do this with only the CPU so I could develop a better understanding of rasterization, lighting/shading (I used Gouraud shading) and texture mapping.

#Building

Open the solution file in Visual Studio 2017, or use the precompiled version in the Debug folder 

#Graphics

The graphics for this project was handled by the CPU (as opposed to using OpenGL / DirectX for graphics). I coded the Rasterization, Gouraud Shading and Texture Mapping following popular algorithms as guides.

#SharpDX

I used SharpDX's Matrix, Vector2, Vector3 and Color4 class. All Matrix math was also handled by SharpDX

