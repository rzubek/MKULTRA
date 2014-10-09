using System;
using System.Collections.Generic;
using System.Linq;
using Prolog;

using UnityEngine;

public class PropInfo : PhysicalObject
{
    /// <summary>
    /// True if this is a container that can hold other things.
    /// </summary>
    public bool IsContainer;

    /// <summary>
    /// True if this satisfies hunger
    /// </summary>
    public bool IsFood;

    /// <summary>
    /// True if this is quenches thirst.
    /// </summary>
    public bool IsBeverage;

    /// <summary>
    /// The word for this type of object
    /// </summary>
    public string CommonNoun;

    public override void Awake()
    {
        base.Awake();
        foreach (var o in Contents)
            o.Container = gameObject;

        if (string.IsNullOrEmpty(CommonNoun))
            CommonNoun = name.ToLower();
        CommonNoun = StringUtils.LastWordOf(CommonNoun);
    }

    public void Start()
    {
        if (!KB.Global.IsTrue("register_prop",
                                gameObject, Symbol.Intern(CommonNoun)))
            throw new Exception("Can't register prop "+name);
    }

    #region Container operations
    public IEnumerable<PhysicalObject> Contents
    {
        get
        {
            foreach (Transform child in transform)
                yield return child.GetComponent<PhysicalObject>();
        }
    } 
    #endregion
}
