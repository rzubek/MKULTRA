using System;
using System.Collections.Generic;
using Prolog;
using UnityEngine;

/*
 * WORKING MEMORY INTERFACE
 * 
 * /perception/docked_with:OBJECT
 * 
 * /motor_root/walking_to:Destination
 * /motor_root/last_action:Action
 * 
 * /event_history/*
 */


/// <summary>
/// Mediates between Prolog code and Unity
/// - Controls locomotion
/// - Updates percepts and efference information in working memory (EL KB)
/// </summary>

[AddComponentMenu("Sims/Sim Controller")]
public class SimController : PhysicalObject
{
    #region Public fields
    /// <summary>
    /// Name the character will go by.
    /// Should be a single word.
    /// </summary>
    public string CharacterName;
    [Popup("woman", "man")]
    public string Type="woman";
    #endregion


    /// <summary>
    /// Whether to log actions as they're taken.
    /// </summary>
    public bool LogActions;

    #region Bindings to other components
#pragma warning disable 649

    [Bind(BindingScope.Global)]
    private TileMap tileMap;
#pragma warning restore 649
    #endregion

    #region Private fields

    private ELNode elRoot;

    private ELNode perceptionRoot;

    private ELNode locationRoot;

    private ELNode lastDestination;

    private ELNode eventHistory;

    private ELNode motorRoot;

    private ELNode physiologicalStates;

    readonly Queue<Structure> eventQueue = new Queue<Structure>();

    /// <summary>
    /// Current path being followed if the character is moving.  Null if no current locomotion goal.
    /// </summary>
    // private TilePath currentPath;
    private float nextUpdate;
    private Vector3? nextPosition;

    private GameObject currentDestination;
    private GameObject currentlyDockedWith;

    /// <summary>
    /// Object being locomoted to, if any.
    /// </summary>
    /// <summary>
    /// Object being locomoted to, if any.
    /// </summary>
    public GameObject CurrentDestination
    {
        get
        {
            return this.currentDestination;
        }
        set
        {
            this.currentDestination = value;
            if (currentDestination == null) {
                motorRoot.DeleteKey(SWalkingTo);
            } else {
                ELNode.Store(motorRoot / SWalkingTo % CurrentDestination);
                ELNode.Store(lastDestination % CurrentDestination);
            }
        }
    }

    /// <summary>
    /// Object with which we're currently docked.
    /// </summary>
    public GameObject CurrentlyDockedWith { 
        get
        {
            return this.currentlyDockedWith;
        }
        set
        {
            this.currentlyDockedWith = value;
            if (currentlyDockedWith == null) {
                perceptionRoot.DeleteKey(SDockedWith);
            } else {
                ELNode.Store(perceptionRoot / SDockedWith % CurrentlyDockedWith);
            }
        }
    }

    /// <summary>
    /// Time to wake character up and ask for an action.
    /// </summary>
    private float? sleepUntil;
    #endregion

    #region Event queue operations
    /// <summary>
    /// True if there are events waiting to be processed.
    /// </summary>
    bool EventsPending
    {
        get
        {
            return this.eventQueue.Count > 0;
        }
    }

    private static readonly object[] NullArgs = { null };
    /// <summary>
    /// Informs character of the specified event.  Does not copy the arguments.
    /// </summary>
    /// <param name="eventType">Type of event (functor of the Prolog structure describing the event)</param>
    /// <param name="args">Other information (arguments to the functor).
    /// WARNING: does not copy arguments, so they must either be ground or not used elsewhere.</param>
    public void QueueEvent(string eventType, params object[] args)
    {
        if (args == null)
            args = NullArgs;
        this.QueueEvent(new Structure(Symbol.Intern(eventType), args));
    }

    /// <summary>
    /// Informs character of the specified event.  Does not copy the eventDescription.
    /// </summary>
    /// <param name="eventDescription">A Prolog term describing the event.
    /// WARNING: does not copy the term, so it must either be ground or not used elsewhere.</param>
    public void QueueEvent(Structure eventDescription)
    {
        this.eventQueue.Enqueue((Structure)Term.CopyInstantiation(eventDescription));
    }

    Structure GetNextEvent()
    {
        return this.eventQueue.Dequeue();
    }
    #endregion

    #region Event handling
    /// <summary>
    /// Calls Prolog on all pending events and initiates any actions it specifies.
    /// </summary>
    private void HandleEvents()
    {
        if (EventsPending)
            this.sleepUntil = null;
        while (EventsPending)
            this.NotifyEvent(this.GetNextEvent());
    }

    /// <summary>
    /// Call into Prolog to respond to EVENTDESCRIPTION
    /// </summary>
    /// <param name="eventDescription">Term representing the event</param>
    private void NotifyEvent(object eventDescription)
    {
        ELNode.Store(eventHistory/Term.CopyInstantiation(eventDescription));
        if (!this.IsTrue(new Structure(SNotifyEvent, eventDescription)))
            Debug.LogError("notify_event/1 failed: "+ISOPrologWriter.WriteToString(eventDescription));
    }

    private static readonly Symbol SNotifyEvent = Symbol.Intern("notify_event");
    #endregion

    #region Unity hooks
    internal void Start()
    {
        updateConcernBids = this.UpdateConcernBids;
        elRoot = this.KnowledgeBase().ELRoot;
        this.perceptionRoot = elRoot / Symbol.Intern("perception");
        this.locationRoot = perceptionRoot / Symbol.Intern("location");
        this.motorRoot = elRoot / Symbol.Intern("motor_state");
        this.physiologicalStates = elRoot / Symbol.Intern("physiological_states");
        this.eventHistory = elRoot / Symbol.Intern("event_history");
        this.lastDestination = elRoot / Symbol.Intern("last_destination");
        ELNode.Store(lastDestination % null);  // Need a placeholder last destination so that /last_destination/X doesn't fail.
        if (string.IsNullOrEmpty(CharacterName))
            CharacterName = name;
        if (!KB.Global.IsTrue("register_character", gameObject, Symbol.Intern(CharacterName), Symbol.Intern((Type))))
            throw new Exception("Can't register character " + name);
    }

    private bool prologInitializationsExecuted;
    private void EnsureCharacterInitialized()
    {
        if (prologInitializationsExecuted)
            return;
        try
        {
            prologInitializationsExecuted = true;
            this.gameObject.IsTrue(Symbol.Intern("do_all_character_initializations"));
        }
        catch (Exception e)
        {
            Debug.LogError("Exception while initializing character " + this.gameObject.name);
            Debug.LogException(e);
        }
    }

    internal void Update()
    {
        if (!PauseManager.Paused)
        {
            this.UpdateLocomotion();

            this.UpdateLocations();

            this.EnsureCharacterInitialized();

            this.HandleEvents();

            this.MaybeDoNextAction();
        }
    }

    internal void OnCollisionEnter2D(Collision2D collision)
    {
        this.QueueEvent("collision", collision.gameObject);
    }
    #endregion

    #region Perception update

    private void UpdateLocations()
    {
        foreach (var p in Registry<PhysicalObject>())
        {
            var o = p.gameObject;
            // Determine if it's inside something
            if (p.Container == null)
            {
                // It's not inside another object, so find what room it's in.
                var n = locationRoot.ChildWithKey(o);
                if (n == null || !n.ExclusiveKeyValue<GameObject>().GetComponent<Room>().Contains(o))
                {
                    foreach (var r in Registry<Room>())
                        if (r.Contains(o))
                        {
                            ELNode.Store(locationRoot / o % (r.gameObject));
                        }
                }
            }
            else
                ELNode.Store((this.locationRoot/o%p.Container));
        }
    }

    #endregion

    #region Primitive actions handled by SimController

    private bool pollActions;
    private void MaybeDoNextAction()
    {
        if (pollActions || !this.sleepUntil.HasValue || this.sleepUntil.Value <= Time.time)
        {
            pollActions = false;
            sleepUntil = null;
            this.DoNextAction();
            this.DecisionCycleCount++;
        }
    }

    public int DecisionCycleCount;

    public int DecisionCycleAlloc;
    void DoNextAction()
    {
        var actionVar = new LogicVariable("Action");

        var beforeBytes = GC.GetTotalMemory(false);
        var action = this.SolveFor(actionVar, new Structure(SNextAction, actionVar));
        var allocBytes = GC.GetTotalMemory(false) - beforeBytes;
        if (allocBytes > 0)
            DecisionCycleAlloc = (int)allocBytes;
        this.InitiateAction(action);
    }

    private static readonly Symbol SNextAction = Symbol.Intern("next_action");

    private static readonly Symbol SWalkingTo = Symbol.Intern("walking_to");

    private static readonly Symbol SLastAction = Symbol.Intern("last_action");

    private void InitiateAction(object action)
    {
        if (action == null)
            return;

        var actionCopy = Term.CopyInstantiation(action);
        ELNode.Store(motorRoot / SLastAction % actionCopy);

        var structure = action as Structure;
        if (structure != null)
        {
            switch (structure.Functor.Name)
            {
                //case "face":
                //    this.Face(structure.Argument<GameObject>(0));
                //    break;

                case "cons":
                    // It's a list of actions to initiate.
                    this.InitiateAction(structure.Argument(0));
                    this.InitiateAction(structure.Argument(1));
                    break;

                case "sleep":
                    this.sleepUntil = Time.time + Convert.ToSingle(structure.Argument(0));
                    break;

                case "pickup":
                {
                    var patient = structure.Argument<GameObject>(0);
                    if (patient == null)
                        throw new NullReferenceException("Argument to pickup is not a gameobject");
                    var physob = patient.GetComponent<PhysicalObject>();
                    if (physob == null)
                        throw new NullReferenceException("Argument to pickup is not a physical object.");
                    physob.MoveTo(gameObject);
                    break;
                }

                case "ingest":
                {
                    var patient = structure.Argument<GameObject>(0);
                    if (patient == null)
                        throw new NullReferenceException("Argument to ingest is not a gameobject");
                    var physob = patient.GetComponent<PhysicalObject>();
                    if (physob == null)
                        throw new NullReferenceException("Argument to ingest is not a physical object.");
                    physob.Destroy();
                    var propinfo = patient.GetComponent<PropInfo>();
                    if (propinfo != null)
                    {
                        if (propinfo.IsFood)
                            this.physiologicalStates.DeleteKey(Symbol.Intern("hungry"));
                        if (propinfo.IsBeverage)
                            this.physiologicalStates.DeleteKey(Symbol.Intern("thirsty"));
                    }
                    break;
                }

                case "putdown":
                {
                    var patient = structure.Argument<GameObject>(0);
                    if (patient == null)
                        throw new NullReferenceException("Argument to putdown is not a gameobject");
                    var physob = patient.GetComponent<PhysicalObject>();
                    if (physob == null)
                        throw new NullReferenceException("Argument to putdown is not a physical object.");
                    var dest = structure.Argument<GameObject>(1);
                    if (dest == null)
                        throw new NullReferenceException("Argument to putdown is not a gameobject");
                    physob.MoveTo(dest);
                    break;
                }

                default: 
                    throw new NotImplementedException(structure.Functor.Name);
                
            }
            if (structure.Functor.Name != "sleep")
                // Report back to the character that the action has occurred.
                QueueEvent(structure);
        }
        else
            throw new InvalidOperationException("Unknown action: " + ISOPrologWriter.WriteToString(action));
    }

    #endregion

    #region Locomotion control
    private static readonly Symbol SDockedWith = Symbol.Intern("docked_with");
    private void UpdateLocomotion()
    {
        this.UpdateLocomotionBidsAndPath();

        if (CurrentlyDockedWith != null && !CurrentlyDockedWith.DockingTiles().Contains(this.transform.position))
        {
            // We were docked with an object, but are not anymore.
            CurrentlyDockedWith = null;
        }

        if (Time.time > this.nextUpdate && this.nextPosition.HasValue) {
            this.transform.position = this.nextPosition.Value;
            OnPathSuccessful();
        }

        //if (this.currentPath != null)
        //{
        //    // Update the steering
        //    if (this.currentPath.UpdateSteering(this.steering)
        //        || (Vector2.Distance(this.transform.position, currentDestination.transform.position) < 0.75
        //             && currentDestination.IsCharacter()))
        //    {
        //        OnPathSuccessful();
        //    }
        //}
    }

    private void OnPathSuccessful () {
        // Finished the path
        this.CurrentlyDockedWith = CurrentDestination;
        this.CurrentDestination = null;
        this.QueueEvent("arrived_at", this.CurrentlyDockedWith);
        this.nextPosition = null;
        this.nextUpdate = float.PositiveInfinity;
        //this.currentPath = null;
    }

    readonly Dictionary<GameObject, float> bidTotals = new Dictionary<GameObject, float>();

    // ReSharper disable once InconsistentNaming
    private readonly Symbol SConcerns = Symbol.Intern("concerns");

    void UpdateLocomotionBidsAndPath()
    {
        //foreach (var pair in bidTotals)
        //    bidTotals[pair.Key] = 0;
        bidTotals.Clear();
        elRoot.WalkTree(SConcerns, this.updateConcernBids);

        GameObject winner = null;
        float winningBid = 0;
        foreach (var pair in bidTotals)
            if (pair.Value > winningBid)
            {
                winningBid = pair.Value;
                winner = pair.Key;
            }

        if (winner != null)
        {
            // Replan if destination has changed or if destination has moved away from current path.
            var newDestination = (winner != CurrentDestination && winner != CurrentlyDockedWith);
            if (newDestination)
            {
                if (newDestination)
                    ELNode.Store(eventHistory / new Structure("goto", winner)); // Log change for debugging purposes.
                this.CurrentDestination = winner;
                this.nextPosition = winner.transform.position;
                this.nextUpdate = Time.time + UnityEngine.Random.value * 5f;
                // this.currentPath = planner.Plan(gameObject.TilePosition(), this.CurrentDestination.DockingTiles());
            }
        }
    }

    private Action<ELNode> updateConcernBids;
    private static readonly Symbol SLocationBids = Symbol.Intern("location_bids");
    void UpdateConcernBids(ELNode concern)
    {
        // Make sure this isn't the EL root (it's not an actual concern node).
        if (concern.Key != null)
        {
            ELNode bids;
            if (concern.TryLookup(SLocationBids, out bids))
            {
                // Add its bids in
                foreach (var bid in bids.Children)
                {
                    var destination = bid.Key as GameObject;
                    if (destination == null)
                        throw new Exception("Location bid is not a GameObject: "+bid.Key);
                    var bidValue = Convert.ToSingle(bid.ExclusiveKeyValue<object>());
                    if (bidTotals.ContainsKey(destination))
                        bidTotals[destination] += bidValue;
                    else
                        bidTotals[destination] = bidValue;
                }
            }
        }
    }
    #endregion

    #region PhysicalObject methods
    public override void Destroy()
    {
        tileMap.SetTileColor(gameObject.DockingTiles(), Color.red);
        base.Destroy();
    }

    #endregion
}
