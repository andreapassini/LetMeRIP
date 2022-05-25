using System;
using System.Linq;
using System.Collections.Generic;

namespace MonoBT {

	// Defer function to actions and conditions
	public delegate bool BTCall();

	// Uniform interface for all tasks
	public interface IBTTask {
		bool Run();
	}

	// A task with a condition to be verified 
	public class BTCondition : IBTTask {
		
		protected BTCall Condition;
		
		public BTCondition(BTCall call) { Condition = call; }
		
		public bool Run() { return Condition(); }
	}

	// A task with an action to perform
	// NOTE: the code here is the same as BTCondition
	//       we just assume a different semantic in the delegate
	public class BTAction : IBTTask {
		
		protected BTCall Action;
		
		public BTAction(BTCall call) { Action = call; }
		
		public bool Run() { return Action(); }
	}

	// Abstract class holding tasks and compose subtasks
	public abstract class BTComposite : IBTTask {
		
		// The list of attached children
		protected IBTTask[] children;
		
		public BTComposite(IBTTask[] tasks) {
			children = tasks;
		}
		
		// as for IBTTask interface
		public abstract bool Run();
	}

	// A task implementing a selector
	public class BTSelector : BTComposite {
		
		public BTSelector(IBTTask[] tasks) : base(tasks) { ; }
		
		override public bool Run() {
			// All children are run until one succeeds
			foreach (IBTTask t in children) {
				if (t.Run()) return true;
			}
			// Otherwise the selector fails
			return false;
		}
	}

	// A task implementing a sequence
	public class BTSequence : BTComposite {
		
		public BTSequence(IBTTask[] tasks) : base(tasks) { ; }
		
		override public bool Run() {
			// All children are run until one fails
			foreach (IBTTask t in children) {
				if (!t.Run()) return false;
			}
			// otherwise the sequence succeeds
			return true;
		}
	}

	// This class will hold the decision structure
	public class BehaviorTree : IBTTask {
		
		protected IBTTask root;
		
		public BehaviorTree(IBTTask task) { root = task; }
		
		public bool Run() { return root.Run(); }
	}

	// Abstract class holding tasks to be run in random order
	public abstract class BTRandomComposite : BTComposite /* , IBTTask */ {

		public BTRandomComposite(IBTTask[] tasks) : base(tasks) { ; }
		
		// Randomize the tasks vector
		public void Shuffle() {
			Random rnd = new Random ();
			children = children.OrderBy (x => rnd.Next ()).ToArray ();
		}
	}

	// A task implementing a random selector
	public class BTRandomSelector : BTRandomComposite {
		
		public BTRandomSelector(IBTTask[] tasks) : base(tasks) { ; }
		
		override public bool Run() {
			Shuffle();
			foreach (IBTTask t in children) {
				if (t.Run()) return true;
			}
			return false;
		}
	}

	// A task implementing a random sequrence
	public class BTRandomSequence : BTRandomComposite {

		public BTRandomSequence(IBTTask[] tasks) : base(tasks) { ; }
		
		override public bool Run() {
			Shuffle();
			foreach (IBTTask t in children) {
				if (!t.Run()) return false;
			}
			return true;
		}
	}

	// Abstract class holding a decorator
	public abstract class BTDecorator : IBTTask {
		
		// Only one child is required
		protected IBTTask child;
		
		public BTDecorator(IBTTask task) { child = task; }
		
		public abstract bool Run();
	}

	// A task implementing a filter decorator
	public class BTDecoratorFilter : BTDecorator {
		
		// Condition to trigger the filter
		private BTCall Condition;
		
		public BTDecoratorFilter(BTCall condition, IBTTask task) : base(task) {
			Condition = condition;
		}
		
		override public bool Run() { return Condition() && child.Run(); }
	}

	// A task implementing a limit decorator
	public class BTDecoratorLimit : BTDecorator {
		
		private int maxRepetitions;
		private int count;
		
		public BTDecoratorLimit(int max, IBTTask task) : base(task) {
			maxRepetitions = max;
			count = 0;
		}

		override public bool Run() {
			if (count < maxRepetitions) {
				count += 1;
				return child.Run();
			}
			return true;
		}	
	}

	// A task implementing an until fail decorator
	public class BTDecoratorUntilFail : BTDecorator {
		
		public BTDecoratorUntilFail(IBTTask task) : base(task) { ; }
		
		override public bool Run() { while(child.Run()); return true; } // mind the semicolon
	}

	// A task implementing an inverter decorator
	public class BTDecoratorInverter : BTDecorator {
		
		public BTDecoratorInverter(IBTTask task) : base(task) { ; }
		
		override public bool Run() { return !child.Run(); }
	}

}


