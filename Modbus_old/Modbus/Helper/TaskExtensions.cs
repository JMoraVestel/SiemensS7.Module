using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusModule.Helper
{
    public static class TaskExtensions
    {
        public static async Task WithCancellation(this Task task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(() => tcs.TrySetResult(true)))
            {
                if (task != await Task.WhenAny(task, tcs.Task))
                    throw new OperationCanceledException(cancellationToken);
            }

            await task; // Propagate any exceptions
        }
    }
}
