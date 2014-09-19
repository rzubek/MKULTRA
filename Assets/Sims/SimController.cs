using System;
using System.Collections.Generic;
using Prolog;
using UnityEngine;

/*
 * WORKING MEMORY INTERFACE
 * 
 * /perception/conversational_space/*
 * /perception/social_space/*
 * /perception/nobody_speaking
 * /perception/docked_with:OBJECT
 * 
 * /motor_root/walking_to:Destination
 * /motor_root/last_action:Action
 * /motor_root/i_am_speaking
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

    #region Constants
    /// <summary>
    /// The radius of the the circular region around the character that defines
    /// its "conversational space".  Two characters having a conversation should
    /// normally be within one anothers' conversational spaces.
    /// </summary>
    private const float ConversationalSpaceRadius = 2;
    private const float SocialSpaceRadius = 4;

    /// <summary>
    /// Maximum number of characters that can be within a character's conversational space.
    /// </summary>
    private const int MaxConversationalSpaceColliders = 30;

    private const float SpeechDelaySecondsPerChar = 0.075f;

    private const float SpeechDelayMinimum = 1f;
    private const float SpeechDelayMaximum = 4f;

    private readonly Symbol playerSymbol = Symbol.Intern("player");
    #endregion

    /// <summary>
    /// Whether to log actions as they're taken.
    /// </summary>
    public bool LogActions;

    /// <summary>
    /// Is this character currently talking?
    /// </summary>
    public bool IsSpeaking
    {
        get
        {
            return currentSpeechBubbleText != null;
        }
    }

    #region Bindings to other components
#pragma warning disable 649
    [Bind]
    private CharacterSteeringController steering;

    [Bind(BindingScope.GameObject, BindingDefault.Ignore)]
    private object nlPrompt;

    [Bind(BindingScope.Global, BindingDefault.Create)]
    private PathPlanner planner;

    [Bind(BindingScope.Global)]
    private TileMap tileMap;
#pragma warning restore 649
    #endregion

    #region Private fields

    private ELNode elRoot;

    private ELNode perceptionRoot;

    private ELNode locationRoot;

    private ELNode conversationalSpace;

    private ELNode socialSpace;

    private ELNode lastDestination;

    private ELNode eventHistory;

    private ELNode motorRoot;

    private ELNode physiologicalStates;

    private Room myCurrentRoom;

    readonly Queue<Structure> eventQueue = new Queue<Structure>();

    /// <summary>
    /// Holds the current text for the character's speech bubble.
    /// Set to null is no active text.
    /// </summary>
    string currentSpeechBubbleText;

    /// <summary>
    /// Time at which currentSpeechBubbleText should be set to null.
    /// </summary>
    private float clearSpeechTime;

    /// <summary>
    /// Current path being followed if the character is moving.  Null if no current locomotion goal.
    /// </summary>
    private TilePath currentPath;

    private GameObject currentDestination;

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
            if (currentDestination == null)
            {
                motorRoot.DeleteKey(SWalkingTo);
            }
            else
                ELNode.Store(motorRoot/SWalkingTo%CurrentDestination);
        }
    }

    /// <summary>
    /// Object with which we're currently docked.
    /// </summary>
    public GameObject CurrentlyDockedWith { get; private set; }

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
        this.conversationalSpace = perceptionRoot / Symbol.Intern("conversational_space");
        this.socialSpace = perceptionRoot / Symbol.Intern("social_space");
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
            this.UpdateSpeechBubble();

            this.UpdateLocomotion();

            this.UpdateLocations();

            this.UpdateSpace(
                conversationalSpaceColliders,
                ConversationalSpaceRadius,
                conversationalSpace,
                "enter_conversational_space",
                "exit_conversational_space",
                true);
            this.UpdateSpace(
                socialSpaceColliders,
                SocialSpaceRadius,
                socialSpace,
                "enter_social_space",
                "exit_social_space",
                false);

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
    readonly Collider2D[] conversationalSpaceColliders = new Collider2D[MaxConversationalSpaceColliders];
    readonly Collider2D[] socialSpaceColliders = new Collider2D[MaxConversationalSpaceColliders];

    // ReSharper disable once InconsistentNaming
    private readonly Symbol SNobodySpeaking = Symbol.Intern("nobody_speaking");
    
    /// <summary>
    /// Update the set of character's within this characters' conversational space
    /// and generate any necessary enter/leave events.
    /// </summary>
    private void UpdateSpace(Collider2D[] colliders, float radius, ELNode statusNode, string enterEvent, string exitEvent, bool updateNobodySpeaking)
    {
        var characterCount = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            radius,
            colliders,
            1 << gameObject.layer);
        if (characterCount==MaxConversationalSpaceColliders)
            throw new Exception("Too many colliders in conversational space!");

        // Clean out entries that are no longer in the area
        statusNode.DeleteAll(
            node =>
            {
                // Look to see if node's key (a character) appears in the colliders
                for (var i = 0; i<characterCount;i++)
                    if (ReferenceEquals(node.Key, colliders[i].gameObject))
                        return false;
                // It doesn't, so the character left this character's conversational space.

                // Tell this character about it
                QueueEvent(exitEvent, node.Key);

                // Remove the character
                return true;
            });

        // Add new entries
        for (var i = 0; i<characterCount;i++)
        {
            var character = colliders[i].gameObject;
            if (character != gameObject && !statusNode.ContainsKey(character) && myCurrentRoom.Contains(character))
            {
                // The character just arrived in this character's conversational space

                // Tell this character
                QueueEvent(enterEvent, character);

                // Update the KB
                ELNode.Store(statusNode/character);
            }
        }

        if (updateNobodySpeaking)
        {
            bool nobodySpeaking = true;
            for (var i = 0; i<characterCount; i++)
                if (colliders[i].GetComponent<SimController>().IsSpeaking)
                    nobodySpeaking = false;
            if (nobodySpeaking)
                ELNode.Store(perceptionRoot/SNobodySpeaking);
            else
            {
                if (perceptionRoot.ContainsKey(SNobodySpeaking))
                {
                    perceptionRoot.DeleteKey(SNobodySpeaking);
                    pollActions = true;
                }
            }
        }
    }

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
                            if (o == gameObject)
                                myCurrentRoom = r;
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
                case "face":
                    this.Face(structure.Argument<GameObject>(0));
                    break;

                case "say":
                    // Say a fixed string
                    this.Say(structure.Argument<string>(0));
                    break;

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
                    // Assume it's dialog
                    var textVar = new LogicVariable("DialogText");
                    object text = null;
                    try
                    {
                        text = gameObject.SolveFor(textVar, "generate_text", structure, textVar);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(string.Format("Exception while generating text for {0}", gameObject.name));
                        Debug.LogException(e);
                    }
                    var textString = text as string;
                    if (textString == null)
                        throw new Exception(
                            "generate_text returned " + ISOPrologWriter.WriteToString(text) + " for "
                            + ISOPrologWriter.WriteToString(structure));
                    var talkingToPlayer = structure.Arity >= 2 && ReferenceEquals(structure.Argument(1), playerSymbol);
                    if (talkingToPlayer)
                        // Character is talking to zhimself
                    {
                        // if (nlPrompt != null)
                        //    nlPrompt.OutputToPlayer(textString);
                        // else
                            this.Say(string.Format("({0})", textString));
                    }
                    else
                        this.Say(textString);

                    if (!talkingToPlayer)
                    {
                        // Tell the other characters
                        foreach (var node in this.socialSpace.Children)
                        {
                            var character = (GameObject)(node.Key);
                            if (character != this.gameObject)
                                character.QueueEvent((Structure)Term.CopyInstantiation(structure));
                        }
                    }
                    break;
            }
            if (structure.Functor.Name != "sleep")
                // Report back to the character that the action has occurred.
                QueueEvent(structure);
        }
        else
            throw new InvalidOperationException("Unknown action: " + ISOPrologWriter.WriteToString(action));
    }

    private void UpdateSpeechBubble()
    {
        // Clear speech bubble if it's time.
        if (this.currentSpeechBubbleText != null && Time.time > this.clearSpeechTime)
        {
            this.currentSpeechBubbleText = null;
            this.motorRoot.DeleteKey(SIAmSpeaking);
        }
    }

    /// <summary>
    /// Turns character to face the specified GameObject
    /// </summary>
    /// <param name="target">Object to face</param>
    public void Face(GameObject target)
    {
        steering.Face(target.Position() - (Vector2)transform.position);
    }

    /// <summary>
    /// Displays the specified string.
    /// </summary>
    /// <param name="speech">String to display</param>
    public void Say(string speech)
    {
        this.currentSpeechBubbleText = speech;
        ELNode.Store(motorRoot / SIAmSpeaking);
        clearSpeechTime = Time.time +
            Math.Max(
                SpeechDelayMinimum,
                Math.Min(SpeechDelayMaximum,
                         speech.Length * SpeechDelaySecondsPerChar));
    }

    // ReSharper disable once InconsistentNaming
    private static readonly Symbol SIAmSpeaking = Symbol.Intern("i_am_speaking");
    #endregion

    #region Locomotion control
    private static readonly Symbol SDockedWith = Symbol.Intern("docked_with");
    private void UpdateLocomotion()
    {
        this.UpdateLocomotionBidsAndPath();

        if (CurrentlyDockedWith != null && !CurrentlyDockedWith.DockingTiles().Contains(this.transform.position))
        {
            // We were docked with an object, but are not anymore.
            perceptionRoot.DeleteKey(SDockedWith);
            CurrentlyDockedWith = null;
        }

        if (this.currentPath != null)
        {
            // Update the steering
            if (this.currentPath.UpdateSteering(this.steering)
                || (Vector2.Distance(this.transform.position, currentDestination.transform.position) < 0.75
                     && currentDestination.IsCharacter()))
            {
                // Finished the path
                this.CurrentlyDockedWith = CurrentDestination;
                ELNode.Store(perceptionRoot/SDockedWith%CurrentlyDockedWith);
                ELNode.Store(lastDestination % this.CurrentDestination);
                this.currentPath = null;
                this.currentDestination = null;
                (motorRoot / SWalkingTo).DeleteSelf();
                this.Face(CurrentlyDockedWith);
                this.steering.Stop();
                this.QueueEvent("arrived_at", this.CurrentlyDockedWith);
            }
        }
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
            if (newDestination
                || (currentDestination != null && currentPath != null && !CurrentDestination.DockingTiles().Contains(currentPath.FinalTile)))
            {
                if (newDestination)
                    ELNode.Store(eventHistory / new Structure("goto", winner)); // Log change for debugging purposes.
                this.CurrentDestination = winner;
                this.currentPath = planner.Plan(gameObject.TilePosition(), this.CurrentDestination.DockingTiles());
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

    #region Speech bubbles
    public GUIStyle SpeechBubbleStyle;

    internal void OnGUI()
    {
        if (Camera.current != null && !string.IsNullOrEmpty(this.currentSpeechBubbleText))
        {
            var bubblelocation = (Vector2)Camera.current.WorldToScreenPoint(transform.position);
            GUI.Label(new Rect(bubblelocation.x, Camera.current.pixelHeight-bubblelocation.y, 300, 300), this.currentSpeechBubbleText, SpeechBubbleStyle);
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
