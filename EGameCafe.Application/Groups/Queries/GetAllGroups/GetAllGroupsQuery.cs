﻿using AutoMapper;
using AutoMapper.QueryableExtensions;
using EGameCafe.Application.Common.Exceptions;
using EGameCafe.Application.Common.Interfaces;
using EGameCafe.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EGameCafe.Application.Groups.Queries.GetAllGroups
{
    public class GetAllGroupsQuery : IRequest<GetAllGroupsVm>
    {
        public GetAllGroupsQuery(int from, int count, string sortType)
        {
            From = from;
            Count = count;
            SortType = sortType;
        }

        public int From { get; set; }
        public int Count { get; set; }
        public string SortType { get; set; }

        public override string ToString()
        {
            return From + Count + SortType;
        }
    }

    public class Handler : IRequestHandler<GetAllGroupsQuery, GetAllGroupsVm>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IMapper _mapper;

        public Handler(IApplicationDbContext context, IMemoryCache cache, IMapper mapper)
        {
            _context = context;
            _cache = cache;
            _mapper = mapper;
        }

        public async Task<GetAllGroupsVm> Handle(GetAllGroupsQuery request, CancellationToken cancellationToken)
        {
            string cacheKey = request.ToString() + nameof(GetAllGroupsQuery);

            if (_cache.TryGetValue(cacheKey, out GetAllGroupsVm cacheData))
            {
                return cacheData;
            }

            var vm = new GetAllGroupsVm();

            vm.TotalGroups = _context.Group.Count();

            switch (request.SortType)
            {
                case "groupname":
                    vm.List = await _context.Group.Include(e=>e.Game).Where(e=>e.GroupType == GroupType.publicGroup).OrderBy(e => e.GroupName).Skip(request.From).Take(request.Count)
                                    .ProjectTo<GetAllGroupsDto>(_mapper.ConfigurationProvider).ToListAsync();
                    break;
                case "grouptype":
                    vm.List = await _context.Group.Include(e => e.Game).Where(e => e.GroupType == GroupType.publicGroup).OrderBy(e => e.GroupType).Skip(request.From).Take(request.Count)
                                    .ProjectTo<GetAllGroupsDto>(_mapper.ConfigurationProvider).ToListAsync();
                    break;

                default:
                    vm.List = await _context.Group.Include(e => e.Game).Where(e => e.GroupType == GroupType.publicGroup).Skip(request.From).Take(request.Count)
                                   .ProjectTo<GetAllGroupsDto>(_mapper.ConfigurationProvider).ToListAsync(); 
                    break;
            }

            if (vm.List.Any())
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromSeconds(10));

                _cache.Set(cacheKey, vm, cacheEntryOptions);

                return vm;
            }

            throw new NotFoundException(nameof(GetAllGroupsQuery), request);

        }
    }
}
