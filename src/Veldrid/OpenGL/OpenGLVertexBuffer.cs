﻿namespace Veldrid.OpenGL
{
    internal class OpenGLVertexBuffer : OpenGLBuffer, VertexBuffer
    {
        public OpenGLVertexBuffer(ref BufferDescription description)
            : base(description.SizeInBytes, description.Dynamic, OpenGLBinding.BufferTarget.ArrayBuffer)
        {
        }
    }
}
