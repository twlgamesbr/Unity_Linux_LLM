using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.PlatformToolkit.PlayMode
{
    internal class PlayModeAccount : IAccount, IDoubleSignOut
    {
        private readonly IPlayModeUserManager m_UserManager;
        private readonly IEnvironment m_Environment;
        private IAchievementSystem m_AchievementSystem;
        private ISavingSystem m_SavingSystem;

        public PlayModeAccountData AccountData { get; private set; }

        /// <summary>
        /// A unique runtime ID that can be used to compare account instances that represent the same conceptual user.
        /// </summary>
        public int AccountId { get; private set; }

        public AccountState State { get; set; }

        public PlayModeAccount(
            IEnvironment environment,
            IPlayModeUserManager userManager,
            PlayModeAccountData associatedData,
            ICapabilities capabilities,
            PlatformToolkitMetrics metrics
        )
        {
            AccountId = MakeAccountId(userManager, associatedData);
            Assert.AreNotEqual(-1, AccountId, "Account data not found in the user manager when it should exist there.");

            State = AccountState.SignedIn;
            AccountData = associatedData;
            m_UserManager = userManager;
            m_Environment = environment;
            m_SavingSystem = new PlayModeSavingSystem(m_Environment, AccountId, associatedData.Saves, metrics);
            m_AchievementSystem = capabilities.AccountAchievements
                ? new PlayModeAchievementSystem(m_Environment, associatedData.Achievements)
                : null;
        }

        private static int MakeAccountId(IPlayModeUserManager userManager, PlayModeAccountData accountData)
        {
            // Make an account ID from the index of the associated data.
            for (int i = 0; i < userManager.AccountData.Count; ++i)
            {
                if (accountData == userManager.AccountData[i])
                    return i;
            }
            return -1;
        }

        public async Task<bool> SignOut()
        {
            await m_Environment.WaitIfPaused();

            bool ret = m_UserManager.CanSignOutAccount(AccountData) == SignOutStatus.Allowed;
            if (ret)
                m_UserManager.SignOutAccount(AccountData);

            return ret;
        }

        public bool HasAttribute<T>(string attributeName)
        {
            return AccountData.AttributeValues.TryGetAttributeValue(attributeName, out T _);
        }

        public async Task<T> GetAttribute<T>(string attributeName)
        {
            await m_Environment.WaitIfPaused();

            if (AccountData.AttributeValues.TryGetAttributeValue(attributeName, out T attributeValue))
            {
                return attributeValue;
            }
            throw new InvalidOperationException("Attribute not found");
        }

        public async Task<string> GetName()
        {
            try
            {
                await m_Environment.WaitIfPaused();
                return AccountData?.PublicName ?? string.Empty;
            }
            catch (Exception e)
            {
                return AccountErrorHandling.HandleGetNameException(e);
            }
        }

        public async Task<Texture2D> GetPicture()
        {
            try
            {
                await m_Environment.WaitIfPaused();
                if (m_Environment.OfflineNetwork)
                {
                    return null;
                }
                return AccountData?.Picture;
            }
            catch (Exception e)
            {
                return AccountErrorHandling.HandleGetPictureException(e);
            }
        }

        public async Task<ISavingSystem> GetSavingSystem()
        {
            await m_Environment.WaitIfPaused();
            return m_SavingSystem;
        }

        public async Task<IAchievementSystem> GetAchievementSystem()
        {
            await m_Environment.WaitIfPaused();
            if (m_AchievementSystem == null)
            {
                throw new InvalidOperationException("Achievement System is not supported on this platform");
            }
            return m_AchievementSystem;
        }

        public bool TrySignOut()
        {
            if (State == AccountState.SignedOut)
                return false;
            State = AccountState.SignedOut;
            return true;
        }

        public Task CleanUpAfterSignOut()
        {
            m_SavingSystem = null;
            m_AchievementSystem = null;
            return Task.CompletedTask;
        }
    }
}
