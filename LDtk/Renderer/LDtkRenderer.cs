namespace LDtk.Renderer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using LDtk;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// Renderer for the ldtkWorld, ldtkLevel, intgrids and entities.
/// This can all be done in your own class if you want to reimplement it and customize it differently
/// this one is mostly here to get you up and running quickly.
/// </summary>
public class LDtkRenderer : IDisposable
{
    /// <summary> Gets or sets the spritebatch used for rendering with this Renderer. </summary>
    public SpriteBatch SpriteBatch { get; set; }

    /// <summary> Gets or sets the levels identifier to layers Dictionary. </summary>
    Dictionary<string, RenderedLevel> PrerenderedLevels { get; } = new();

    /// <summary> Gets or sets the levels identifier to layers Dictionary. </summary>
    Dictionary<string, Texture2D> TilemapCache { get; } = new();

    readonly Texture2D pixel;
    readonly Texture2D error;
    readonly GraphicsDevice graphicsDevice;
    readonly ContentManager? content;

    /// <summary> Initializes a new instance of the <see cref="LDtkRenderer"/> class. This is used to intizialize the renderer for use with direct file loading. </summary>
    /// <param name="spriteBatch">Spritebatch</param>
    public LDtkRenderer(SpriteBatch spriteBatch)
    {
        SpriteBatch = spriteBatch;
        graphicsDevice = spriteBatch.GraphicsDevice;

        if (pixel == null)
        {
            pixel = new Texture2D(graphicsDevice, 1, 1);
            pixel.SetData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
        }

        if (error == null)
        {
            error = new Texture2D(graphicsDevice, 2, 2);
            error.SetData(
                new byte[]
                {
                    0xFF, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0xFF,
                    0xFF, 0xFF, 0x00, 0xFF, 0xFF, 0x00, 0x00, 0x00,
                }
            );
        }
    }

    /// <summary> Initializes a new instance of the <see cref="LDtkRenderer"/> class. This is used to intizialize the renderer for use with content Pipeline. </summary>
    /// <param name="spriteBatch">SpriteBatch</param>
    /// <param name="content">Optional ContentManager</param>
    public LDtkRenderer(SpriteBatch spriteBatch, ContentManager content)
        : this(spriteBatch)
    {
        this.content = content;
    }

    /// <summary> Prerender out the level to textures to optimize the rendering process. </summary>
    /// <param name="level">The level to prerender.</param>
    /// <exception cref="Exception">The level already has been prerendered.</exception>
    public void PrerenderLevel(LDtkLevel level)
    {
        if (PrerenderedLevels.ContainsKey(level.Identifier))
        {
            return;
        }

        RenderedLevel renderLevel = new();

        SpriteBatch.Begin(samplerState: SamplerState.PointClamp);
        {
            renderLevel.Layers = RenderLayers(level);
        }

        SpriteBatch.End();

        PrerenderedLevels.Add(level.Identifier, renderLevel);
        graphicsDevice.SetRenderTarget(null);
    }

    /// <summary> Prerender out a specific layer from a level to a texture to optimize the rendering process. </summary>
    /// <param name="layer">The layer to prerender.</param>
    /// <param name="level">The level the layer belongs to.</param>
    /// <exception cref="Exception">The layer already has been prerendered.</exception>
    public void PrerenderLayer(LayerInstance layer, LDtkLevel level)
    {
        string identifier = layer.Iid.ToString();
        if (PrerenderedLevels.ContainsKey(identifier))
        {
            return;
        }

        RenderedLevel renderLayer = new();

        SpriteBatch.Begin(samplerState: SamplerState.PointClamp);
        {
            Texture2D renderedLayer = RenderLayer(layer, level);
            renderLayer.Layers = new[] { renderedLayer };
        }

        SpriteBatch.End();

        PrerenderedLevels.Add(identifier, renderLayer);
        graphicsDevice.SetRenderTarget(null);
    }

    Texture2D[] RenderLayers(LDtkLevel level)
    {
        List<Texture2D> layers = new();

        if (level.BgRelPath != null)
        {
            layers.Add(RenderBackgroundToLayer(level));
        }

        if (level.LayerInstances == null)
        {
            return layers.ToArray();
        }

        layers.AddRange(RenderLayers(level.LayerInstances, level));

        return layers.ToArray();
    }
    
    Texture2D[] RenderLayers(LayerInstance[] layers, LDtkLevel level)
    {
        List<Texture2D> results = new();
        
        // Render Tile, Auto and Int grid layers
        for (int i = layers.Length - 1; i >= 0; i--)
        {
            LayerInstance layer = layers[i];

            if (layer.TilesetRelPath == null)
            {
                continue;
            }

            if (layer.Type == LayerType.Entities)
            {
                continue;
            }
            results.Add(RenderLayer(layer, level));
        }

        return results.ToArray();
    }

    Texture2D RenderLayer(LayerInstance layer, LDtkLevel level)
    {
        Texture2D texture = GetTexture(level, layer.TilesetRelPath);

        int width = layer.CellWidth * layer.GridSize;
        int height = layer.CellHeight * layer.GridSize;
        RenderTarget2D renderTarget = new(graphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

        graphicsDevice.SetRenderTarget(renderTarget);

        switch (layer.Type)
        {
            case LayerType.Tiles:
            foreach (TileInstance tile in layer.GridTiles.Where(_ => layer.TilesetDefUid.HasValue))
            {
                Vector2 position = new(tile.Position.X + layer.PixelTotalOffsetX, tile.Position.Y + layer.PixelTotalOffsetY);
                Rectangle rect = new(tile.Src.X, tile.Src.Y, layer.GridSize, layer.GridSize);
                SpriteEffects mirror = (SpriteEffects)tile.FlipBits;
                SpriteBatch.Draw(texture, position, rect, Color.White, 0, Vector2.Zero, 1f, mirror, 0);
            }
            break;

            case LayerType.AutoLayer:
            case LayerType.IntGrid:
            if (layer.AutoLayerTiles.Length > 0)
            {
                foreach (TileInstance tile in layer.AutoLayerTiles.Where(_ => layer.TilesetDefUid.HasValue))
                {
                    Vector2 position = new(tile.Position.X + layer.PixelTotalOffsetX, tile.Position.Y + layer.PixelTotalOffsetY);
                    Rectangle rect = new(tile.Src.X, tile.Src.Y, layer.GridSize, layer.GridSize);
                    SpriteEffects mirror = (SpriteEffects)tile.FlipBits;
                    SpriteBatch.Draw(texture, position, rect, Color.White, 0, Vector2.Zero, 1f, mirror, 0);
                }
            }
            break;
        }

        return renderTarget;
    }

    Texture2D RenderBackgroundToLayer(LDtkLevel level)
    {
        Texture2D texture = GetTexture(level, level.BgRelPath);

        RenderTarget2D layer = new(graphicsDevice, level.PixelWidth, level.PixelHeight, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

        graphicsDevice.SetRenderTarget(layer);
        {
            LevelBackgroundPosition? bg = level.BgPos;
            if (bg != null)
            {
                Vector2 pos = bg.TopLeftPixel.ToVector2();
                SpriteBatch.Draw(texture, pos, new Rectangle((int)bg.CropRect[0], (int)bg.CropRect[1], (int)bg.CropRect[2], (int)bg.CropRect[3]), Color.White, 0, Vector2.Zero, bg.Scale, SpriteEffects.None, 0);
            }
        }
        graphicsDevice.SetRenderTarget(null);

        return layer;
    }

    Texture2D GetTexture(LDtkLevel level, string? path)
    {
        if (path == null)
        {
            return error;
        }

        if (TilemapCache.TryGetValue(path, out Texture2D? texture))
        {
            return texture;
        }

        Texture2D tilemap;
        if (content == null)
        {
            string directory = Path.GetDirectoryName(level.WorldFilePath)!;
            string assetName = Path.Join(directory, path);
            tilemap = Texture2D.FromFile(graphicsDevice, assetName);
        }
        else
        {
            string file = Path.ChangeExtension(path, null);
            string directory = Path.GetDirectoryName(level.WorldFilePath)!;
            string assetName = Path.Join(directory, file);
            tilemap = content.Load<Texture2D>(assetName);
        }

        TilemapCache.Add(path, tilemap);

        return tilemap;
    }

    /// <summary> Render the prerendered level you created from PrerenderLevel(). </summary>
    /// <param name="level">Level to prerender</param>
    /// <exception cref="LDtkException"></exception>
    public void RenderPrerenderedLevel(LDtkLevel level)
    {
        if (PrerenderedLevels.TryGetValue(level.Identifier, out RenderedLevel prerenderedLevel))
        {
            for (int i = 0; i < prerenderedLevel.Layers.Length; i++)
            {
                SpriteBatch.Draw(prerenderedLevel.Layers[i], level.Position.ToVector2(), Color.White);
            }
        }
        else
        {
            throw new LDtkException($"No prerendered level with Identifier {level.Identifier} found.");
        }
    }

    /// <summary> Render the prerendered layer you created from PrerenderLayer(). </summary>
    /// <param name="layer">Layer to prerender</param>
    /// <param name="level">Level the layer belongs to</param>
    /// <exception cref="LDtkException"></exception>
    public void RenderPrerenderedLayer(LayerInstance layer, LDtkLevel level)
    {
        string identifier = layer.Iid.ToString();
        if (PrerenderedLevels.TryGetValue(identifier, out RenderedLevel prerenderedLayer))
        {
            foreach (var texture in prerenderedLayer.Layers)
            {
                SpriteBatch.Draw(texture, level.Position.ToVector2(), Color.White);
            }
        }
        else
        {
            throw new LDtkException($"No prerendered layer with Identifier {identifier} found.");
        }
    }

    /// <summary> Render the level directly without prerendering the layers alot slower than prerendering. </summary>
    /// <param name="level">Level to render</param>
    public void RenderLevel(LDtkLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        Texture2D[] layers = RenderLayers(level);

        for (int i = 0; i < layers.Length; i++)
        {
            SpriteBatch.Draw(layers[i], level.Position.ToVector2(), Color.White);
        }
    }

    /// <summary> Render intgrids by displaying the intgrid as solidcolor squares. </summary>
    /// <param name="intGrid">Render intgrid</param>
    public void RenderIntGrid(LDtkIntGrid intGrid)
    {
        for (int x = 0; x < intGrid.GridSize.X; x++)
        {
            for (int y = 0; y < intGrid.GridSize.Y; y++)
            {
                int cellValue = intGrid.Values[(y * intGrid.GridSize.X) + x];

                if (cellValue != 0)
                {
                    // Color col = intGrid.GetColorFromValue(cellValue);
                    int spriteX = intGrid.WorldPosition.X + (x * intGrid.TileSize);
                    int spriteY = intGrid.WorldPosition.Y + (y * intGrid.TileSize);
                    SpriteBatch.Draw(pixel, new Vector2(spriteX, spriteY), null, Color.Pink /*col*/, 0, Vector2.Zero, new Vector2(intGrid.TileSize), SpriteEffects.None, 0);
                }
            }
        }
    }

    /// <summary> Renders the entity with the tile it includes. </summary>
    /// <param name="entity">The entity you want to render.</param>
    /// <param name="texture">The spritesheet/texture for rendering the entity.</param>
    public void RenderEntity<T>(T entity, Texture2D texture)
        where T : ILDtkEntity
    {
        SpriteBatch.Draw(texture, entity.Position, entity.Tile, Color.White, 0, entity.Pivot * entity.Size, 1, SpriteEffects.None, 0);
    }

    /// <summary> Renders the entity with the tile it includes. </summary>
    /// <param name="entity">The entity you want to render.</param>
    /// <param name="texture">The spritesheet/texture for rendering the entity.</param>
    /// <param name="flipDirection">The direction to flip the entity when rendering.</param>
    public void RenderEntity<T>(T entity, Texture2D texture, SpriteEffects flipDirection)
        where T : ILDtkEntity
    {
        SpriteBatch.Draw(texture, entity.Position, entity.Tile, Color.White, 0, entity.Pivot * entity.Size, 1, flipDirection, 0);
    }

    /// <summary> Renders the entity with the tile it includes. </summary>
    /// <param name="entity">The entity you want to render.</param>
    /// <param name="texture">The spritesheet/texture for rendering the entity.</param>
    /// <param name="animationFrame">The current frame of animation. Is a very basic entity animation frames must be to the right of them and be the same size.</param>
    public void RenderEntity<T>(T entity, Texture2D texture, int animationFrame)
        where T : ILDtkEntity
    {
        Rectangle animatedTile = entity.Tile;
        animatedTile.Offset(animatedTile.Width * animationFrame, 0);
        SpriteBatch.Draw(texture, entity.Position, animatedTile, Color.White, 0, entity.Pivot * entity.Size, 1, SpriteEffects.None, 0);
    }

    /// <summary> Renders the entity with the tile it includes. </summary>
    /// <param name="entity">The entity you want to render.</param>
    /// <param name="texture">The spritesheet/texture for rendering the entity.</param>
    /// <param name="flipDirection">The direction to flip the entity when rendering.</param>
    /// <param name="animationFrame">The current frame of animation. Is a very basic entity animation frames must be to the right of them and be the same size.</param>
    public void RenderEntity<T>(T entity, Texture2D texture, SpriteEffects flipDirection, int animationFrame)
        where T : ILDtkEntity
    {
        Rectangle animatedTile = entity.Tile;
        animatedTile.Offset(animatedTile.Width * animationFrame, 0);
        SpriteBatch.Draw(texture, entity.Position, animatedTile, Color.White, 0, entity.Pivot * entity.Size, 1, flipDirection, 0);
    }

    /// <summary> Dispose Renderer </summary>
    public void Dispose()
    {
        pixel.Dispose();
        GC.SuppressFinalize(this);
    }
}
