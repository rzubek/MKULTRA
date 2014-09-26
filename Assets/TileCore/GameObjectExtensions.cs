using UnityEngine;

public static class GameObjectExtensions
{
    /// <summary>
    /// The object's position as a Vector2
    /// </summary>
    /// <param name="o">GameObject</param>
    /// <returns>Its position</returns>
    public static Vector2 Position(this GameObject o)
    {
        return o.transform.position;
    }
 
    /// <summary>
    /// Returns the parent GameObject of this GameObject
    /// </summary>
    /// <param name="o">The GameObject to get the parent of</param>
    /// <returns>The parent GameObject</returns>
    public static GameObject GetParent(this GameObject o)
    {
        return o.transform.gameObject;
    }
}
