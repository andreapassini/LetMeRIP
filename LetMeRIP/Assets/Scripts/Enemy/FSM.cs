using System;
using System.Linq;
using System.Collections.Generic;

// Defer function to trigger activation condition
// Returns true when transition can fire
public delegate bool FSMCondition();

// Defer function to perform action
public delegate void FSMAction();

public class FSMTransition {

	// The method to evaluate if the transition is ready to fire
	public FSMCondition myCondition;

	// A list of actions to perform when this transition fires
	private List<FSMAction> myActions = new List<FSMAction>();

	public FSMTransition(FSMCondition condition, FSMAction[] actions = null) {
		myCondition = condition;
		if (actions != null) myActions.AddRange(actions);
	}

	// Call all  actions
	public void Fire() {
		foreach (FSMAction action in myActions) action();
	}
}

public class FSMState {

	// Arrays of actions to perform based on transitions fire (or not)
	// Getters and setters are preferable, but we want to keep the source clean
	public List<FSMAction> enterActions = new List<FSMAction> ();
	public List<FSMAction> stayActions = new List<FSMAction> ();
	public List<FSMAction> exitActions = new List<FSMAction> ();

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
	public void Enter() { foreach (FSMAction a in enterActions) a(); }
	public void Stay() { foreach (FSMAction a in stayActions) a(); }
	public void Exit() { foreach (FSMAction a in exitActions) a(); }

}

public class FSM {

	// Current state
	public FSMState current;

	public FSM(FSMState state) {
		current = state;
		current.Enter();
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
			current.Exit();		// 1
			transition.Fire();	// 2
			current = current.NextState(transition);	// 3
			current.Enter();	// 4
		} else {
			current.Stay();		// 5
		}
	}
}