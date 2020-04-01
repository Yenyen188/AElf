using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Types;
using Acs0;
using Acs3;
using AElf.Kernel.CodeCheck.Application;
using AElf.Kernel.Proposal.Application;
using AElf.CSharp.Core.Extension;
using AElf.Kernel.SmartContract.Application;

namespace AElf.Kernel.CodeCheck
{
    public class CodeCheckRequiredLogEventProcessor : IBlocksExecutionSucceededLogEventProcessor
    {
        private readonly ISmartContractAddressService _smartContractAddressService;
        private readonly ICodeCheckService _codeCheckService;
        private LogEvent _interestedEvent;
        private readonly IProposalService _proposalService;

        public LogEvent InterestedEvent
        {
            get
            {
                if (_interestedEvent != null)
                    return _interestedEvent;

                var address = _smartContractAddressService.GetZeroSmartContractAddress();

                _interestedEvent = new CodeCheckRequired().ToLogEvent(address);

                return _interestedEvent;
            }
        }

        public CodeCheckRequiredLogEventProcessor(ISmartContractAddressService smartContractAddressService,
            ICodeCheckService codeCheckService, IProposalService proposalService)
        {
            _smartContractAddressService = smartContractAddressService;
            _codeCheckService = codeCheckService;
            _proposalService = proposalService;
        }

        public Task ProcessAsync(Block block, Dictionary<TransactionResult, List<LogEvent>> logEventsMap)
        {
            foreach (var events in logEventsMap)
            {
                var transactionResult = events.Key;
                foreach (var logEvent in events.Value)
                {
                    // a new task for time-consuming code check job 
                    Task.Run(async () =>
                    {
                        var eventData = new CodeCheckRequired();
                        eventData.MergeFrom(logEvent);
                        var codeCheckResult = await _codeCheckService.PerformCodeCheckAsync(
                            eventData.Code.ToByteArray(),
                            transactionResult.BlockHash, transactionResult.BlockNumber, eventData.Category);
                        if (!codeCheckResult)
                            return;

                        var proposalId = ProposalCreated.Parser
                            .ParseFrom(transactionResult.Logs.First(l => l.Name == nameof(ProposalCreated)).NonIndexed)
                            .ProposalId;
                        // Cache proposal id to generate system approval transaction later
                        _proposalService.AddNotApprovedProposal(proposalId, transactionResult.BlockNumber);
                    });
                }
            }

            return Task.CompletedTask;
        }
    }
}