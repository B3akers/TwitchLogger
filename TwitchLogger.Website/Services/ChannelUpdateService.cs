using TwitchLogger.Website.Interfaces;

namespace TwitchLogger.Website.Services
{
    public class ChannelUpdateService : BackgroundService
    {
        private readonly IChannelRepository _channelRepository;
        public ChannelUpdateService(IChannelRepository channelRepository)
        {
            _channelRepository = channelRepository;
        }
        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await _channelRepository.UpdateChannels();
                await Task.Delay(1000 * 3600 * 24, stoppingToken);
            }
        }
    }
}
