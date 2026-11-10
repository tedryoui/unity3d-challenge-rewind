using System.Threading;
using UnityEngine;

namespace DefaultNamespace
{
    public interface IRewind
    {
        bool CanRewind { get; }

        CancellationToken LifetimeToken { get; }

        void OnRewindStarted();

        void OnRewindCompleted();

        /// <summary>Mono mode: starts a per-object awaitable rewind.</summary>
        Awaitable RewindMono(CancellationToken token);

        /// <summary>Job mode: builds a snapshot entry that the system schedules into a job.</summary>
        JobRewindEntry CreateJobEntry();

        void Clear();
    }
}

