using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// A map (background) built out of tiles in a TileSet (a kind of sprite sheet).
/// </summary>
public class TileMap : BindingBehaviour
{
    public static string WallTileRegex = "^Wall top";
    #region Map data
    private Tile[,] contents;

    private SpriteRenderer[,] renderers;

    public int MapRows { get; private set; }

    public int MapColumns { get; private set; }

    public Tile this[int column, int row]
    {
        get
        {
            return contents[column, row] ?? (contents[column, row] = new Tile());
        }
    }

    public Tile this[TilePosition p]
    {
        get
        {
            return this[p.Column, p.Row];
        }
    }

    IEnumerable<TilePosition> TilePositions()
    {
        for (int i=0; i<MapColumns; i++)
            for (int j = 0; j < MapRows; j++)
            {
                if (contents[i,j] != null)
                    yield return new TilePosition(i, j);
            }
    }
    #endregion

    #region Initialization
    public override void Awake()
    {
        base.Awake();

        var allSprites = this.GetComponentsInChildren<SpriteRenderer>();
        this.GetMapDimensions(allSprites);
        this.PopulateMap(allSprites);

        MarkObstacles();
    }

    /// <summary>
    /// Mark all tiles of all sprites for which IsStaticObstacle() is true.
    /// </summary>
    private void MarkObstacles()
    {
        foreach (var sprite in FindObjectsOfType<SpriteRenderer>())
        {
            if (IsStaticObstacle(sprite))
            {
                foreach (var tile in sprite.gameObject.FootprintTiles())
                {
                    this[tile].Type = TileType.Obstacle;
                }
                UpdateSortingOrder(sprite);
            }
        }
    }

    public static void UpdateSortingOrder(SpriteRenderer sprite)
    {
        var minY = sprite.bounds.min.y;
        sprite.sortingOrder = (int)(-100*minY);
    }

    /// <summary>
    /// Test if this is an unmovable obstacle like furniture or an appliance.
    /// True if this is not a map tile, it has a box collider, and it doesn't have a RigidBody2D.
    /// </summary>
    private bool IsStaticObstacle(SpriteRenderer sprite)
    {
        if (sprite.sortingLayerName == "Map")
            return false;
        if (sprite.GetComponent<BoxCollider2D>() == null)
            return false;
        return sprite.GetComponent<Rigidbody2D>() == null;
    }

    // ReSharper disable ParameterTypeCanBeEnumerable.Local
    private void PopulateMap(SpriteRenderer[] allSprites)
        // ReSharper restore ParameterTypeCanBeEnumerable.Local
    {
        var wall = new Regex(WallTileRegex);
        foreach (var spriteRenderer in allSprites)
        {
            TilePosition p = spriteRenderer.bounds.center;
            var tile = this[p];
            var spriteName = spriteRenderer.sprite.name;
            tile.SpriteName = spriteName;
            tile.Type = wall.IsMatch(spriteName) ? TileType.Wall : TileType.Freespace;
            renderers[p.Column, p.Row] = spriteRenderer;
        }
    }

    public void SetTileColor(TilePosition p, Color c)
    {
        var tileRenderer = renderers[p.Column, p.Row];
        if (tileRenderer != null)
            tileRenderer.color = c;
    }

    public void SetTileColor(TileRect r, Color c)
    {
        foreach (var tile in r)
            SetTileColor(tile, c);
    }

    private void GetMapDimensions(SpriteRenderer[] allSprites)
    {
        float tileSize = 2*allSprites[0].bounds.extents.x;
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        foreach (var spriteRenderer in allSprites)
        {
            var bounds = spriteRenderer.bounds;
            if (Mathf.Abs((bounds.max.x-bounds.min.x)-tileSize) > 0.01
                || Mathf.Abs((bounds.max.y - bounds.min.y) - tileSize) > 0.01)
                throw new Exception("Map appears to have tiles of differing sizes");
            minX = Mathf.Min(minX, bounds.min.x);
            minY = Mathf.Min(minY, bounds.min.y);
            maxX = Mathf.Max(maxX, bounds.max.x);
            maxY = Mathf.Max(maxY, bounds.max.y);
        }

        Tile.SizeInSceneUnits = tileSize;
        Tile.MapXMin = minX;
        Tile.MapYMin = minY;
        MapColumns = Mathf.RoundToInt((maxY - minY) / tileSize);
        MapRows = Mathf.RoundToInt((maxX - minX) / tileSize);
        contents = new Tile[MapColumns, MapRows];
        renderers = new SpriteRenderer[MapColumns, MapRows];
    }
    #endregion

    #region Contents tracking

    public bool IsFreespace(TilePosition p)
    {
        if (p.Column < 0 || p.Row < 0 || p.Column >= MapColumns || p.Row >= MapRows)
            return false;
        return true;
    }
    #endregion


}
