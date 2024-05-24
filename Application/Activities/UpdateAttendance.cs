using Application.Core;
using Application.Interfaces;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence;

namespace Application.Activities
{
    public class UpdateAttendance
    {
        public class Command : IRequest<Result<Unit>?>
        {
            public Guid Id { get; set; }
        }

        public class Handler : IRequestHandler<Command, Result<Unit>?>
        {
            private readonly DataContext _context;
            private readonly IUserAccessor _userAccessor;
            public Handler(DataContext context, IUserAccessor userAccessor)
            {
                _context = context;
                _userAccessor = userAccessor;
            }

            public async Task<Result<Unit>?> Handle(Command request, CancellationToken cancellationToken)
            {
                Activity? activity = await _context.Activities.Include(a => a.Attendees).ThenInclude(u => u.AppUser).FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken: cancellationToken);

                if (activity == null)
                    return null;

                AppUser? user = await _context.Users.FirstOrDefaultAsync(x => x.UserName == _userAccessor.GetUsername(), cancellationToken: cancellationToken);

                if (user == null)
                    return null;

                string? hostUsername = activity.Attendees.FirstOrDefault(x => x.IsHost)?.AppUser?.UserName;

                ActivityAttendee? attendance = activity.Attendees.FirstOrDefault(x => x.AppUser.UserName == user.UserName);

                if (attendance != null && hostUsername == user.UserName)
                    activity.IsCancelled = !activity.IsCancelled;

                if (attendance != null && hostUsername != user.UserName)
                    activity.Attendees.Remove(attendance);

                if (attendance == null)
                {
                    attendance = new ActivityAttendee
                    {
                        AppUser = user,
                        Activity = activity,
                        IsHost = false,
                    };

                    activity.Attendees.Add(attendance);
                }

                bool result = await _context.SaveChangesAsync(cancellationToken) > 0;

                return result ? Result<Unit>.Success(Unit.Value) : Result<Unit>.Failure("Problem updating attendance");
            }
        }
    }
}
