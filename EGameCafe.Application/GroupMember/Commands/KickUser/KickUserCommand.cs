﻿using EGameCafe.Application.Common.Exceptions;
using EGameCafe.Application.Common.Interfaces;
using EGameCafe.Application.Common.Models;
using EGameCafe.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace EGameCafe.Application.GroupMembers.Commands.KickUser
{
    public class KickUserCommand : IRequest<Result>
    {
        public string GroupId { get; set; }
        public string UserId { get; set; }

    }

    public class Handler : IRequestHandler<KickUserCommand, Result>
    {
        private readonly IApplicationDbContext _context;
        public Handler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Result> Handle(KickUserCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.GroupMember.FirstOrDefaultAsync(e => e.UserId == request.UserId && e.GroupId == request.GroupId);

            if (entity == null)
            {
                throw new NotFoundException(nameof(Group), request.GroupId);
            }

            entity.IsBlock = true;

            _context.GroupMember.Update(entity);

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success(entity.GroupMemberId);
        }
    }
}
