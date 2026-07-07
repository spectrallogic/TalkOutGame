using System.Threading.Tasks;
using TalkOut.Data;

namespace TalkOut.Core
{
    /// Plays the physical side of an action (walk, pose, prop animation, camera).
    /// M1 has no implementation (narration-only turns); the 3D WorldPerformer
    /// plugs in at M3 without touching the turn loop.
    public interface ISceneActionPerformer
    {
        Task PerformAsync(ActionDefinition action);
    }
}
