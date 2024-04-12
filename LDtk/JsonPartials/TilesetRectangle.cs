namespace LDtk;

using Microsoft.Xna.Framework;

#pragma warning disable CA2225

public partial class TilesetRectangle
{
    /// <summary> Monogame Implicit Rectangle Cast. </summary>
    public static implicit operator Rectangle(TilesetRectangle r)
    {
        return new Rectangle(r.X, r.Y, r.Width, r.Height);
    }
}

#pragma warning restore
