using System;
using Rummy.Util;

namespace Rummy.AI;

public static class DecisionTree
{
    public interface INode
    {
        public INode Traverse(object state, out object result);
        public void ValidateTypes(Type stateType, Type resultType);
    }

    public static class Nodes
    {
        public record Decision<TState>(Func<TState, bool> Predicate, INode Success, INode Failure) : INode
        {
            public INode Traverse(object state, out object result) { result = null; return Predicate((TState)state) ? Success : Failure; }
            public void ValidateTypes(Type stateType, Type resultType) {
                if (stateType != typeof(TState)) throw new InvalidCastException("Decision node state type does not match state type of tree.");
            }
        }

        public record Leaf<TResult>(TResult Result) : INode where TResult : class
        {
            public INode Traverse(object state, out object result) { result = Result; return null; }
            public void ValidateTypes(Type stateType, Type resultType) {
                if (resultType != typeof(TResult)) throw new InvalidCastException("Leaf node result type does not match result type of tree.");
            }
        }
    }

    public static Nodes.Decision<TState> Decision<TState>(Func<TState, bool> predicate) => new(predicate, null, null);
    public static Nodes.Leaf<TResult> Leaf<TResult>(TResult result) where TResult : class => new(result);
}

public record DecisionTree<TState, TResult>(DecisionTree.INode Root) where TResult : class
{
    public TResult Traverse(TState state) {
        TResult result; DecisionTree.INode currentNode = Root;
        do {
            currentNode.ValidateTypes(typeof(TState), typeof(TResult));
            currentNode = currentNode.Traverse(state, out object resultObj);
            result = resultObj as TResult;
        }
        while (currentNode is not null);

        if (result is null) throw new Exception("DecisionTree incorrectly structured. Branch ended on a non-leaf node.");
        return result!;
    }
}