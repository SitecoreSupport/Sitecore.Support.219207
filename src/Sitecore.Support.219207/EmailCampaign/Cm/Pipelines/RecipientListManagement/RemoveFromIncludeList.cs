namespace Sitecore.Support.EmailCampaign.Cm.Pipelines.RecipientListManagement
{
    using System.Collections.Generic;
    using System.Linq;
    using Sitecore.Data;
    using Sitecore.Diagnostics;
    using Sitecore.EmailCampaign.Cm.Pipelines.RecipientListManagement;
    using Sitecore.ExM.Framework.Diagnostics;
    using Sitecore.Modules.EmailCampaign;
    using Sitecore.Modules.EmailCampaign.Core;
    using Sitecore.Modules.EmailCampaign.Exceptions;
    using Sitecore.Modules.EmailCampaign.Factories;
    using Sitecore.Modules.EmailCampaign.ListManager;
    using Sitecore.Modules.EmailCampaign.RecipientCollections;
    using Sitecore.Modules.EmailCampaign.Recipients;
    using Sitecore.Modules.EmailCampaign.Xdb;

    public class RemoveFromIncludeList
    {
        private readonly ILogger _logger;
        private readonly Factory _factory;
        private readonly EcmFactory _ecmFactory;

        public RemoveFromIncludeList([NotNull] ILogger logger)
            : this(Factory.Instance, EcmFactory.GetDefaultFactory(), logger)
        {
        }

        internal RemoveFromIncludeList([NotNull] Factory factory, [NotNull] EcmFactory ecmfactory, [NotNull] ILogger logger)
        {
            Assert.ArgumentNotNull(factory, "factory");
            Assert.ArgumentNotNull(ecmfactory, "ecmfactory");
            Assert.ArgumentNotNull(logger, "logger");

            _factory = factory;
            _ecmFactory = ecmfactory;
            _logger = logger;
        }

        public void Process([NotNull] RecipientListManagementPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

            var recipientManager = _factory.GetRecipientManager(args.MessageItem.InnerItem);
            var includedContactListIds = new HashSet<ID>(recipientManager.IncludedRecipientListIds);
            var recipientId = new XdbContactId(args.ContactId);
            var recipient = RecipientRepository.GetDefaultInstance().GetRecipientSpecific(recipientId, typeof(ContactListAssociations));

            if (recipient == null)
            {
                throw new EmailCampaignException(EcmTexts.UserNotExisits, recipientId);
            }

            var contactListAssociations = recipient.GetProperties<ContactListAssociations>();
            if (contactListAssociations == null || contactListAssociations.DefaultProperty == null)
            {
                throw new EmailCampaignException(EcmTexts.NoListAssociationsForContact, recipientId);
            }

            var listIdsToRemoveContactFrom = includedContactListIds
                .Intersect(contactListAssociations.DefaultProperty.ContactListIds)
                .Where(listid => recipientManager.GetRecipientList(listid).Type == "ContactList");

            try
            {
                bool removedFromAny = false;
                bool listsExist = false;

                foreach (var listId in listIdsToRemoveContactFrom)
                {
                    listsExist = true;
                    bool removed = _ecmFactory.Bl.SubscriptionManager.RemoveUserFromList(recipientId, listId);
                    if (removed)
                    {
                        removedFromAny = true;
                    }
                }

                if (!listsExist)
                {
                    _logger.LogDebug($"There are no available non-segmented lists for contact '{args.ContactId}' and message '{args.MessageId}' to be removed from");
                    return;
                }

                if (!removedFromAny)
                {
                    throw new MessageEventPipelineException(string.Format("Failed to remove  contact '{0}' from any include lists", args.ContactId));
                }
            }
            catch (OperationTimeoutException ex)
            {
                _logger.LogError(string.Format("Failed to remove contact '{0}' from include list(s)", args.ContactId), ex);
                throw;
            }
        }
    }
}