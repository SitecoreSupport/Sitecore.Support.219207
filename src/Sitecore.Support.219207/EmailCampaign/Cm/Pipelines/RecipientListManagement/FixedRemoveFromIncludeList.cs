namespace Sitecore.Support.EmailCampaign.Cm.Pipelines.RecipientListManagement
{
    using System.Collections.Generic;
    using System.Linq;
    using Data;
    using Diagnostics;
    using ExM.Framework.Diagnostics;
    using Modules.EmailCampaign;
    using Modules.EmailCampaign.Factories;
    using Modules.EmailCampaign.ListManager;
    using Modules.EmailCampaign.RecipientCollections;
    using Modules.EmailCampaign.Recipients;
    using Modules.EmailCampaign.Xdb;
    using Sitecore.EmailCampaign.Cm.Pipelines.RecipientListManagement;
    using Sitecore.EmailCampaign.Model.Exceptions;

    public class FixedRemoveFromIncludeList : RemoveFromIncludeList
    {
        private readonly EcmFactory _ecmFactory;

        private readonly Factory _factory;
        private readonly ILogger _logger;

        public FixedRemoveFromIncludeList(ILogger logger)
            : this(Factory.Instance, EcmFactory.GetDefaultFactory(), logger)
        {
        }

        internal FixedRemoveFromIncludeList(Factory factory, EcmFactory ecmfactory, ILogger logger) : base(logger)
        {
            Assert.ArgumentNotNull(factory, "factory");
            Assert.ArgumentNotNull(ecmfactory, "ecmfactory");
            Assert.ArgumentNotNull(logger, "logger");
            _factory = factory;
            _ecmFactory = ecmfactory;
            _logger = logger;
        }

        public void Process(RecipientListManagementPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            var first = new HashSet<ID>(_factory.GetRecipientManager(args.MessageItem.InnerItem)
                .IncludedRecipientListIds);
            var xdbContactId = new XdbContactId(args.ContactId);
            var recipientSpecific = RecipientRepository.GetDefaultInstance()
                .GetRecipientSpecific(xdbContactId, typeof(ContactListAssociations));
            if (recipientSpecific == null)
                throw new EmailCampaignException("The recipient '{0}' does not exist.", xdbContactId);
            var properties = recipientSpecific.GetProperties<ContactListAssociations>();
            if (properties != null && properties.DefaultProperty != null)
            {
                var enumerable = first.Intersect(properties.DefaultProperty.ContactListIds);
                try
                {
                    var flag = false;
                    var list = enumerable as IList<ID> ?? enumerable.ToList();
                    foreach (var item in list)
                        if (_ecmFactory.Bl.SubscriptionManager.RemoveUserFromList(xdbContactId, item))
                            flag = true;

                    #region FIX:Added Warning instead of Error when there are no lists to unsibscribe from.
                    //Updated per Conservative Party request to use not display this warning
                    if (list.Count == 0)
                        Log.Debug(
                            string.Format(
                                "An attempt to remove contact '{0}' from include lists without specifying include list ids was registered. Contact will be added to Global-Opt-Out list instead.",
                                args.ContactId), this);
                    else if (!flag)
                        throw new MessageEventPipelineException(string.Format(
                            "Failed to remove  contact '{0}' from any include lists. Lists: '{1}'", args.ContactId,
                            string.Join(",", list)));

                    #endregion
                }


                catch (OperationTimeoutException e)
                {
                    _logger.LogError($"Failed to remove contact '{args.ContactId}' from include list(s)", e);
                    throw;
                }

                return;
            }

            throw new EmailCampaignException("No lists associated with the contact '{0}'.", xdbContactId);
        }
    }
}