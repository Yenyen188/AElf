using System.Threading.Tasks;
using AElf.Kernel.Blockchain.Events;
using AElf.Kernel.Events;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace AElf.CrossChain
{
    internal class CrossChainModuleEventHandler : ILocalEventHandler<NewIrreversibleBlockFoundEvent>, ILocalEventHandler<InitialSyncFinishedEvent>, ITransientDependency
    {
        private readonly ICrossChainService _crossChainService;
        private readonly ICrossChainIndexingDataService _crossChainIndexingDataService;
            
        public CrossChainModuleEventHandler(ICrossChainService crossChainService, 
            ICrossChainIndexingDataService crossChainIndexingDataService)
        {
            _crossChainService = crossChainService;
            _crossChainIndexingDataService = crossChainIndexingDataService;
        }
        
        public async Task HandleEventAsync(InitialSyncFinishedEvent eventData)
        {
            await _crossChainService.FinishInitialSyncAsync();
        }

        public async Task HandleEventAsync(NewIrreversibleBlockFoundEvent eventData)
        {
            await _crossChainService.UpdateWithLib(eventData.BlockHash, eventData.BlockHeight);
            _crossChainIndexingDataService.UpdateCrossChainDataWithLib(eventData.BlockHash, eventData.BlockHeight);
        }
    }
}