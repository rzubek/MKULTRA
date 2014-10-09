﻿using UnityEngine;

public abstract class PhysicalObject : BindingBehaviour
{
    [HideInInspector]
    public GameObject Container;

    /// <summary>
    /// True if this object has not been destroyed.
    /// </summary>
    public bool Exists = true;

    public void MoveTo(GameObject newContainer)
    {
        Container = newContainer;
        // Reparent our gameObject to newContainer
        // Because Unity is braindamaged, this has to be done by way of the transform.
        transform.parent = newContainer.transform;
        var physicalObject = newContainer.GetComponent<PhysicalObject>();
        if (physicalObject != null)
            physicalObject.ObjectAdded(this.gameObject);
    }

    public bool ContentsVisible;

    public void ObjectAdded(GameObject newObject)
    {
        if (newObject.renderer != null)
        {
            newObject.renderer.enabled = ContentsVisible;
            var sr = newObject.renderer as SpriteRenderer;
            if (sr != null && ContentsVisible)
                sr.sortingLayerName = "PlacedOnSurface";
        }
        newObject.transform.localPosition = Vector3.zero;
    }

    public virtual void Destroy()
    {
        this.Exists = false;
        this.MoveTo(GameObject.Find("DestroyedObjects"));
        this.Container = null; // override
        this.enabled = false;
        this.renderer.enabled = false;
    }
}
