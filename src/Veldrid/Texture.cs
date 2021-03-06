﻿using System;

namespace Veldrid
{
    public abstract class Texture : DeviceResource, IDisposable
    {
        public abstract PixelFormat Format { get; }
        public abstract uint Width { get; }
        public abstract uint Height { get; }
        public abstract uint Depth { get; }
        public abstract uint MipLevels { get; }
        public abstract uint ArrayLayers { get; }
        public abstract TextureUsage Usage { get; }
        public abstract void Dispose();
    }
}
