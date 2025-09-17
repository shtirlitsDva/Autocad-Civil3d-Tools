using System;
using System.Collections.Generic;

namespace DimensioneringV2.StateMachine
{
    /// <summary>
    /// A small, reusable finite-state machine with generic states and events.
    /// Effects are injected as delegates and receive transition context plus optional payload.
    /// </summary>
    internal sealed class StateMachine<TState, TEvent>
        where TState : notnull
        where TEvent : notnull
    {
        private readonly Dictionary<(TState State, TEvent Event), Transition> _transitions = new();

        public TState CurrentState { get; private set; }

        public StateMachine(TState initial)
        {
            CurrentState = initial;
        }

        public void Configure(TState from, TEvent ev, TState to, Action<TransitionContext>? effect = null)
        {
            _transitions[(from, ev)] = new Transition(to, effect);
        }

        public void Fire(TEvent ev, object? payload = null)
        {
            if (!_transitions.TryGetValue((CurrentState, ev), out var tr))
            {
                return; // no-op if transition is not defined
            }

            var from = CurrentState;
            var to = tr.Next;
            CurrentState = to;
            tr.Effect?.Invoke(new TransitionContext(from, ev, to, payload));
        }

        public readonly struct TransitionContext
        {
            public TransitionContext(TState from, TEvent ev, TState to, object? payload)
            {
                From = from;
                Event = ev;
                To = to;
                Payload = payload;
            }
            public TState From { get; }
            public TEvent Event { get; }
            public TState To { get; }
            public object? Payload { get; }
        }

        private readonly struct Transition
        {
            public Transition(TState next, Action<TransitionContext>? effect)
            {
                Next = next;
                Effect = effect;
            }
            public TState Next { get; }
            public Action<TransitionContext>? Effect { get; }
        }
    }
}


