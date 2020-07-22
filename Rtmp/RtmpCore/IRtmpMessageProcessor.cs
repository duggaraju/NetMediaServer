using System.Threading.Tasks;

namespace RtmpCore
{
    public interface IRtmpMessageProcessor
    {
        Task ProcessMessageAsync(RtmpMessage message);
    }
}
