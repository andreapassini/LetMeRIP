using System;
using System.Collections.Generic;

// Interface for both decisions and actions
// Any node belonging to the decision tree must be walkable
public interface IDTNode {
	DTAction Walk();
}

// This delegate will defer functions to both
// making decisions and performing actions
public delegate object DTCall(object bundle);

// Decision node
public class DTDecision : IDTNode {

	// The method to call to take the decision
	private DTCall Selector;

	// The return value of the decision is checked against
	// a dictionary and the corresponding link is followed
	private Dictionary<object, IDTNode> links;

	public DTDecision(DTCall selector) {
		Selector = selector;
		links = new Dictionary<object, IDTNode>();
	}

	// Add an entry in the dictionary linking a possible output
	// of Selector to a node
	public void AddLink(object value, IDTNode next) {
		links.Add(value, next);
	}

	// We call the selector and check if there is a matching link
	// for the return value. In such case, we Walk() on the link
	// No link means no action and null is returned
	public DTAction Walk() {
		object o = Selector(null);
		return links.ContainsKey(o) ? links[o].Walk() : null;
	}
}

// Action node
public class DTAction : IDTNode {

	// The methos to perform the action
	public DTCall Action;

	public DTAction(DTCall callee) {
		Action = callee;
	}

	// We are an action, we are the one to be called
	public DTAction Walk() { return this; }
}

// This class is holding our decision structure
public class DecisionTree {

	private IDTNode root;

	// Create a decision tree with starting from a root node
	public DecisionTree(IDTNode start) {
		root = start;
	}

	// Walk the structure and call the resulting action (if any)
	// a null means no action is required.
	public object walk() {
		DTAction result = root.Walk();
		if (result != null) return result.Action(null);
		return null;
	}
}
