﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdCampaign.BLL.Services.Adverts.DTO;
using AdCampaign.Common;
using AdCampaign.DAL.Entities;
using AdCampaign.DAL.Repositories;
using AdCampaign.DAL.Repositories.Adverts;
using AdCampaign.DAL.Repositories.AdvertsStatistic;

namespace AdCampaign.BLL.Services.Adverts
{
    public class AdvertService : IAdvertService
    {
        private readonly IAdvertRepository advertRepository;
        private readonly IAdvertStatisticRepository statisticRepository;
        private readonly IFileRepository fileRepository;

        public AdvertService(IAdvertRepository advertRepository,
            IAdvertStatisticRepository statisticRepository, IFileRepository fileRepository)
        {
            this.advertRepository = advertRepository;
            this.statisticRepository = statisticRepository;
            this.fileRepository = fileRepository;
        }

        public async Task<Result<IEnumerable<ShowAdvertDto>>> GetAdvertsToShow(CancellationToken ct)
        {
            //количество показываемых кампаний
            const int toShowCount = 4;
            var adverts = await advertRepository.Get(new GetAdvertsParams
            {
                IsBlocked = false,
                IsVisible = true,
                ImpressingDate = DateTime.UtcNow,
                ImpressingTime = DateTime.UtcNow.TimeOfDay,
                ToTake = toShowCount,
                Shuffle = true
            });

            var toShow = adverts
                .Select(x => new ShowAdvertDto(x))
                .ToList();

            if (ct.IsCancellationRequested)
                return toShow;

            await statisticRepository.Increment(toShow.Select(x => x.Id), AdvertStatisticType.Impression);
            return toShow;
        }

        public Task IncrementAdvertsStats(long id, AdvertStatisticType statisticType)
        {
            return statisticRepository.Increment(id, statisticType);
        }

        public async Task<Result<Advert>> Get(long userId, long id)
        {
            var advert = await advertRepository.Get(id);
            if (advert == null)
                return new Error("Кампания не найдена", "campaign-not-found");

            return advert;
        }

        public async Task<Result<Advert>> Create(long userId, AdvertDto dto, File primaryImage, File secondaryImage)
        {
            var primaryCreated = primaryImage != null
                ? await fileRepository.Create(primaryImage.Name, primaryImage.Content)
                : null;
            var secondaryCreated = secondaryImage != null
                ? await fileRepository.Create(secondaryImage.Name, secondaryImage.Content)
                : null;

            var advert = new Advert
            {
                Name = dto.Name,
                OwnerId = userId,
                IsBlocked = false,
                IsVisible = dto.IsVisible,
                DateCreated = DateTime.UtcNow,
                DateUpdated = DateTime.UtcNow,
                RequestType = dto.RequestType,
                ImpressingDateFrom = dto.ImpressingDateFrom,
                ImpressingDateTo = dto.ImpressingDateTo,
                ImpressingTimeFrom = dto.ImpressingTimeFrom,
                ImpressingTimeTo = dto.ImpressingTimeTo,
                ImpressingAlways = dto.ImpressingAlways,
                PrimaryImageId = primaryCreated?.Id,
                SecondaryImageId = secondaryCreated?.Id,
                AdvertStatistics = new List<AdvertStatistic>
                {
                    new() {AdvertStatisticType = AdvertStatisticType.Filled},
                    new() {AdvertStatisticType = AdvertStatisticType.Impression},
                    new() {AdvertStatisticType = AdvertStatisticType.Followed},
                }
            };
            return await advertRepository.Insert(advert);
        }

        public async Task<Result<Advert>> Update(long userId, AdvertDto dto, File primaryImage, File secondaryImage)
        {
            var advert = await advertRepository.Get(dto.Id);
            if (advert == null)
                return new Error("Кампания не найдена", "campaign-not-found");

            //todo optimize file updating
            if (primaryImage != null)
            {
                if (advert.PrimaryImage != null)
                    await fileRepository.Delete(advert.PrimaryImage);
                var created = await fileRepository.Create(primaryImage.Name, primaryImage.Content);
                advert.PrimaryImageId = created.Id;
            }

            if (secondaryImage != null)
            {
                if (advert.SecondaryImage != null)
                    await fileRepository.Delete(advert.SecondaryImage);
                var created = await fileRepository.Create(secondaryImage.Name, secondaryImage.Content);
                advert.SecondaryImageId = created.Id;
            }

            UpdateBaseInfo(advert, dto);
            advert.DateUpdated = DateTime.UtcNow;
            return await advertRepository.Update(advert);
        }

        private void UpdateBaseInfo(Advert advert, AdvertDto dto)
        {
            advert.Name = dto.Name;
            advert.IsVisible = dto.IsVisible;
            advert.RequestType = dto.RequestType;
            advert.ImpressingDateFrom = dto.ImpressingDateFrom;
            advert.ImpressingDateTo = dto.ImpressingDateTo;
            advert.ImpressingTimeFrom = dto.ImpressingTimeFrom;
            advert.ImpressingTimeTo = dto.ImpressingTimeTo;
            advert.ImpressingAlways = dto.ImpressingAlways;
        }

        public async Task<Result> Delete(long id, string userEmail, Role role)
        {
            var advert = await advertRepository.Get(id);
            if (CanDeleteByRole(role) || UserIsOwner(advert.Owner, userEmail))
            {
                await advertRepository.Delete(id);
                return new();
            }

            return new Error("У вас нет прав на удаление", "403");

            static bool CanDeleteByRole(Role role) => role == Role.Administrator || role == Role.Moderator;
            static bool UserIsOwner(User user, string email) => user.Email.Equals(email, StringComparison.Ordinal);
        }

        public async Task<Result> ChangeBlock(long userId, Role role, long id, bool block)
        {
            var advert = await advertRepository.Get(id);
            if (CanBlockByRole(role))
            {
                advert.IsBlocked = block;
                if (block)
                {
                    advert.BlockedById = userId;
                    advert.BlockedDate = DateTime.UtcNow;
                }
                else
                {
                    advert.BlockedById = null;
                    advert.BlockedDate = null;
                }

                await advertRepository.Update(advert);
                return new();
            }

            return new Error("У вас нет прав на блокировку", "403");

            static bool CanBlockByRole(Role role) => role == Role.Administrator || role == Role.Moderator;
        }
    }
}