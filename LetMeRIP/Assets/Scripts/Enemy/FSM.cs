using System;
using System.Linq;
using System.Collections.Generic;

// Defer function to trigger activation condition
// Returns true when transition can fire
public delegate bool FSMCondition();

// Defer function to perform action
public delegate void FSMAction();
public delegate void FSMSpecialAction(Object caller);

public class FSMTransition {

	// The method to evaluate if the transition is ready to fire
	public FSMCondition myCondition;

	// A list of actions to perform when this transition fires
	private List<FSMAction> myActions = new List<FSMAction>();
	private List<FSMSpecialAction> mySpecialActions = new List<FSMSpecialAction>();

	public FSMTransition(FSMCondition condition, FSMAction[] actions = null) {
		myCondition = condition;
		if (actions != null) myActions.AddRange(actions);
	}

	public FSMTransition(FSMCondition condition, FSMSpecialAction[] specialActions = null)
	{
		myCondition = condition;
		if (specialActions != null) mySpecialActions.AddRange(specialActions);
	}

	// Call all  actions
	public void Fire() {
		foreach (FSMAction action in myActions) action();
	}

	public void FireSpecialActions(Object caller)
	{
		foreach (FSMSpecialAction specialAction in mySpecialActions) specialAction(caller);
	}
}

public class FSMState {

	// Arrays of actions to perform based on transitions fire (or not)
	// Getters and setters are preferable, but we want to keep the source clean
	public List<FSMAction> enterActions = new List<FSMAction> ();
	public List<FSMAction> stayActions = new List<FSMAction> ();
	public List<FSMAction> exitActions = new List<FSMAction> ();

	public List<FSMSpecialAction> enterSpecialActions = new List<FSMSpecialAction>();
	public List<FSMSpecialAction> staySpecialActions = new List<FSMSpecialAction>();
	public List<FSMSpecialAction> exitSpecialActions = new List<FSMSpecialAction>();

	// A dictionary of transitions and the states they are leading to
	private Dictionary<FSMTransition, FSMState> links;

	public FSMState() {
		links = new Dictionary<FSMTransition, FSMState>();
	}

	public void AddTransition(FSMTransition transition, FSMState target) {
		links [transition] = target;
	}

	public FSMTransition VerifyTransitions() {
		foreach (FSMTransition t in links.Keys) {
			if (t.myCondition()) return t;
		}
		return null;
	}

	public FSMState NextState(FSMTransition t) {
		return links [t];
	}
	
	// These methods will perform the actions in each list
	public void Enter() { 
		foreach (FSMAction a in enterActions) a();
	}
	public void Stay() { 
		foreach (FSMAction a in stayActions) a();
	}
	public void Exit() { 
		foreach (FSMAction a in exitActions) a();
	}


	// These methods will perform the Special Actions in each list
	public void EnterSpecialActions(Object obj)
	{
		foreach (FSMSpecialAction sa in enterSpecialActions) {
			sa(obj);
		}
	}
	public void StaySpecialActions(Object obj)
	{
		foreach (FSMSpecialAction sa in enterSpecialActions) {
			sa(obj);
		}
	}

	public void ExitSpecialActions(Object obj)
	{
		foreach (FSMSpecialAction sa in enterSpecialActions) {
			sa(obj);
		}
	}

}

public class FSM {

	public Object caller;

	// Current state
	public FSMState current;

	public FSM(FSMState state) {
		current = state;
		current.Enter();
	}

	public FSM(FSMState state, Object o)
	{
		current = state;
		current.Enter();
		caller = o;
	}

	// Examine transitions leading out from the current state
	// If a condition is activated, then:
	// (1) Execute actions associated to exit from the current state
	// (2) Execute actions associated to the firing transition
	// (3) Retrieve the new state and set is as the current one
	// (4) Execute actions associated to entering the new current state
	// Otherwise, if no condition is activated,
	// (5) Execute actions associated to staying into the current state

	public void Update() { // NOTE: this is NOT a MonoBehaviour
		FSMTransition transition = current.VerifyTransitions ();
		if (transition != null) {
			current.Exit();     // 1
			current.ExitSpecialActions(caller);
			transition.Fire();  // 2
			transition.FireSpecialActions(caller);
			current = current.NextState(transition);	// 3
			current.Enter();    // 4
			current.EnterSpecialActions(caller);
		} else {
			current.Stay();     // 5
			current.StaySpecialActions(caller);
		}
	}
}