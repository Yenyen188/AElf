﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using AElf.Contracts.Genesis;
using AElf.CrossChain.Communication.Grpc;
using AElf.Kernel;
using AElf.Kernel.Consensus.AEDPoS;
using AElf.Kernel.SmartContract;
using AElf.Kernel.SmartContract.Application;
using AElf.Kernel.SmartContract.Parallel;
using AElf.Kernel.Token;
using AElf.Modularity;
using AElf.OS;
using AElf.OS.Network.Grpc;
using AElf.OS.Node.Application;
using AElf.OS.Node.Domain;
using AElf.Runtime.CSharp;
using AElf.RuntimeSetup;
using AElf.WebApp.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.AspNetCore;
using Volo.Abp.Modularity;
using Volo.Abp.Threading;

namespace AElf.Blockchains.BasicBaseChain
{
    [DependsOn(
        typeof(KernelAElfModule),
        typeof(AEDPoSAElfModule),
        typeof(TokenKernelAElfModule),
        typeof(OSAElfModule),
        typeof(AbpAspNetCoreModule),
        typeof(CSharpRuntimeAElfModule),
        typeof(GrpcNetworkModule),

        typeof(RuntimeSetupAElfModule),
        typeof(GrpcCrossChainAElfModule),

        //web api module
        typeof(WebWebAppAElfModule),

        typeof(ParallelExecutionModule),
        typeof(BlockTransactionLimitControllerModule)
    )]
    public class BasicBaseChainAElfModule : AElfModule
    {
        public OsBlockchainNodeContext OsBlockchainNodeContext { get; set; }

        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            var configuration = context.Services.GetConfiguration();
            var hostBuilderContext = context.Services.GetSingletonInstanceOrNull<HostBuilderContext>();

            var chainType = context.Services.GetConfiguration().GetValue("ChainType", ChainType.MainChain);
            var netType = context.Services.GetConfiguration().GetValue("NetType", NetType.MainNet);

            var newConfig = new ConfigurationBuilder().AddConfiguration(configuration)
                .AddJsonFile($"appsettings.{chainType}.{netType}.json")
                .SetBasePath(context.Services.GetHostingEnvironment().ContentRootPath)
                .Build();

            hostBuilderContext.Configuration = newConfig;
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var configuration = context.Services.GetConfiguration();
            Configure<EconomicOptions>(configuration.GetSection("Economic"));
            Configure<ChainOptions>(option =>
            {
                option.ChainId =
                    ChainHelper.ConvertBase58ToChainId(context.Services.GetConfiguration()["ChainId"]);
                option.ChainType = context.Services.GetConfiguration().GetValue("ChainType", ChainType.MainChain);
                option.NetType = context.Services.GetConfiguration().GetValue("NetType", NetType.MainNet);
            });

            Configure<HostSmartContractBridgeContextOptions>(options =>
            {
                options.ContextVariables[ContextVariableDictionary.NativeSymbolName] = context.Services
                    .GetConfiguration().GetValue("Economic:Symbol", "ELF");
                options.ContextVariables[ContextVariableDictionary.PayTxFeeSymbolList] = context.Services
                    .GetConfiguration()
                    .GetValue("Economic:SymbolListToPayTxFee", "WRITE,READ,STORAGE,TRAFFIC");
                options.ContextVariables[ContextVariableDictionary.PayRentalSymbolList] = context.Services
                    .GetConfiguration().GetValue("Economic:SymbolListToPayRental", "CPU,RAM,DISK,NET");
            });

            Configure<ContractOptions>(configuration.GetSection("Contract"));
            Configure<ContractOptions>(options =>
            {
                options.GenesisContractDir = Path.Combine(context.Services.GetHostingEnvironment().ContentRootPath,
                    "genesis");
                options.ContractFeeStrategyAcsList = new List<string> {"acs1", "acs8"};
            });
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var chainOptions = context.ServiceProvider.GetService<IOptionsSnapshot<ChainOptions>>().Value;
            var dto = new OsBlockchainNodeContextStartDto()
            {
                ChainId = chainOptions.ChainId,
                ZeroSmartContract = typeof(BasicContractZero)
            };

            var dtoProvider = context.ServiceProvider.GetRequiredService<IGenesisSmartContractDtoProvider>();

            dto.InitializationSmartContracts = dtoProvider.GetGenesisSmartContractDtos().ToList();
            var contractOptions = context.ServiceProvider.GetService<IOptionsSnapshot<ContractOptions>>().Value;
            dto.ContractDeploymentAuthorityRequired = contractOptions.ContractDeploymentAuthorityRequired;

            var osService = context.ServiceProvider.GetService<IOsBlockchainNodeContextService>();
            var that = this;
            AsyncHelper.RunSync(async () => { that.OsBlockchainNodeContext = await osService.StartAsync(dto); });
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            var osService = context.ServiceProvider.GetService<IOsBlockchainNodeContextService>();
            var that = this;
            AsyncHelper.RunSync(() => osService.StopAsync(that.OsBlockchainNodeContext));
        }
    }
}