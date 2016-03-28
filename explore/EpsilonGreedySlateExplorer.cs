﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public sealed class EpsilonGreedySlateState
    {
        [JsonProperty(PropertyName = "e")]
        public float Epsilon { get; set; }

        [JsonProperty(PropertyName = "r")]
        public uint[] Ranking { get; set; }

        [JsonProperty(PropertyName = "isExplore")]
        public bool IsExplore { get; set; }
    }

    public sealed class EpsilonGreedySlateExplorer<TContext, TMapperState> : BaseExplorer<TContext, uint[], EpsilonGreedySlateState, uint[], TMapperState>
    {        
        private readonly float defaultEpsilon;

        /// <summary>
        /// The constructor is the only public member, because this should be used with the MwtExplorer.
        /// </summary>
        /// <param name="defaultPolicy">A default function which outputs an action given a context.</param>
        /// <param name="epsilon">The probability of a random exploration.</param>
        /// <param name="numActions">The number of actions to randomize over.</param>
        public EpsilonGreedySlateExplorer(IRanker<TContext, TMapperState> defaultPolicy, float epsilon, uint numActions = uint.MaxValue)
            : base(defaultPolicy, numActions)
        {
            this.defaultEpsilon = epsilon;
        }

        protected override Decision<uint[], EpsilonGreedySlateState, uint[], TMapperState> MapContextInternal(ulong saltedSeed, TContext context, uint numActionsVariable)
        {
            // Invoke the default policy function to get the action
            Decision<uint[], TMapperState> policyDecisionTuple = this.defaultPolicy.MapContext(context, numActionsVariable);

            MultiActionHelper.ValidateActionList(policyDecisionTuple.Value);

            var random = new PRG(saltedSeed);
            float epsilon = explore ? this.defaultEpsilon : 0f;

            uint[] chosenAction;

            bool isExplore;

            if (random.UniformUnitInterval() < 1f - epsilon)
            {
                chosenAction = Enumerable.Range(0, (int)numActionsVariable).Select(u => (uint)u).ToArray();

                for (int i = 0; i < numActionsVariable - 1; i++)
			    {
                    int swapIndex = (int)random.UniformInt((uint)i, numActionsVariable - 1);

                    uint temp = chosenAction[swapIndex];
                    chosenAction[swapIndex] = chosenAction[i];
                    chosenAction[i] = temp;
			    }

                isExplore = true;
            }
            else
            {
                chosenAction = policyDecisionTuple.Value;
                isExplore = false;
            }

            EpsilonGreedySlateState explorerState = new EpsilonGreedySlateState 
            { 
                Epsilon = this.defaultEpsilon, 
                IsExplore = isExplore,
                Ranking = policyDecisionTuple.Value
            };

            return Decision.Create(chosenAction, explorerState, policyDecisionTuple, true);
        }
    }
}