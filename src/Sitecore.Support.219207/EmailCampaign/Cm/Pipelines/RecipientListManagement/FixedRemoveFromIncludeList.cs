
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

    public class FixedRemoveFromIncludeList:RemoveFromIncludeList
  {
      private readonly ILogger _logger;

      private readonly Factory _factory;

      private readonly EcmFactory _ecmFactory;

      public FixedRemoveFromIncludeList(ILogger logger)
          : this(Factory.Instance, EcmFactory.GetDefaultFactory(), logger)
      {
      }

      internal FixedRemoveFromIncludeList(Factory factory, EcmFactory ecmfactory, ILogger logger):base(logger)
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
          HashSet<ID> first = new HashSet<ID>(_factory.GetRecipientManager(args.MessageItem.InnerItem).IncludedRecipientListIds);
          XdbContactId xdbContactId = new XdbContactId(args.ContactId);
          Recipient recipientSpecific = RecipientRepository.GetDefaultInstance().GetRecipientSpecific(xdbContactId, typeof(ContactListAssociations));
          if (recipientSpecific == null)
          {
              throw new EmailCampaignException("The recipient '{0}' does not exist.", xdbContactId);
          }
          PropertyCollection<ContactListAssociations> properties = recipientSpecific.GetProperties<ContactListAssociations>();
          if (properties != null && properties.DefaultProperty != null)
          {
              IEnumerable<ID> enumerable = first.Intersect(properties.DefaultProperty.ContactListIds);
              try
              {
                  bool flag = false;
                  IList<ID> list = (enumerable as IList<ID>) ?? enumerable.ToList();
                  foreach (ID item in list)
                  {
                      if (_ecmFactory.Bl.SubscriptionManager.RemoveUserFromList(xdbContactId, item))
                      {
                          flag = true;
                      }
                  }

                
                  if (!flag)
                  {
                      throw new MessageEventPipelineException(string.Format("Failed to remove  contact '{0}' from any include lists. Lists: '{1}'", args.ContactId, string.Join(",", list)));
                  }
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