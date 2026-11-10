using System.Threading;
using UnityEngine;

namespace DefaultNamespace
{
    public abstract class RewindTransformProvider
    {
        public abstract Awaitable Rewind(CancellationToken cts);
    }
}