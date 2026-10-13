using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using RouteWise.Api.Data;
using RouteWise.Api.Models;

namespace RouteWise.Tests.Data;

public sealed class PersistenceTests
{
    private SqliteConnection connection = null!;
    private RouteWiseDbContext db = null!;

    [SetUp]
    public void SetUp()
    {
        connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();
        db = new RouteWiseDbContext(new DbContextOptionsBuilder<RouteWiseDbContext>()
            .UseSqlite(connection).Options);
        db.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        db.Dispose();
        connection.Dispose();
    }

    [Test]
    public void SavedJourneys_PreserveOccurrenceMetadataAndGeometry()
    {
        var owner = CreateUser("River");
        var original = CreateJourney(owner);
        var copy = CreateJourney(owner);
        db.Journeys.AddRange(original, copy);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        var saved = db.Journeys.Include(j => j.Points).Include(j => j.Segments)
            .Single(j => j.Id == original.Id);
        var points = saved.Points.OrderBy(p => p.Position).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(db.JourneyPoints.Count(), Is.EqualTo(4));
            Assert.That(points.Select(p => p.Id), Is.EqualTo(new[] { "start", "return" }));
            Assert.That(points.Select(p => p.Note), Is.EqualTo(new[] { "Start note", "Return note" }));
            Assert.That(points[1].PlannedTime, Is.EqualTo(new TimeOnly(15, 30)));
            Assert.That(saved.Date, Is.EqualTo(new DateOnly(2026, 10, 9)));
            Assert.That(saved.Segments.Single().Path.Select(p => p.Lat),
                Is.EqualTo(new[] { -33.86, -33.87 }));
        });
    }

    [Test]
    public void AccountDeletion_CascadesWithoutDeletingOtherOwnersData()
    {
        var owner = CreateUser("River");
        var recipient = CreateUser("Avery");
        var otherRecipient = CreateUser("Quinn");
        var owned = CreateJourney(owner);
        var otherOwned = CreateJourney(recipient);
        db.Journeys.AddRange(owned, otherOwned);
        db.Invitations.AddRange(
            Invite(owned, owner, recipient),
            Invite(otherOwned, recipient, owner),
            Invite(otherOwned, recipient, otherRecipient));
        db.SaveChanges();
        db.ChangeTracker.Clear();

        // ExecuteDelete checks database cascades without loading dependants.
        db.Users.Where(u => u.Id == owner.Id).ExecuteDelete();

        Assert.Multiple(() =>
        {
            Assert.That(db.Users.Count(), Is.EqualTo(2));
            Assert.That(db.Journeys.Single().Id, Is.EqualTo(otherOwned.Id));
            Assert.That(db.JourneyPoints.Count(), Is.EqualTo(2));
            Assert.That(db.JourneySegments.Count(), Is.EqualTo(1));
            Assert.That(db.Invitations.Single().RecipientId, Is.EqualTo(otherRecipient.Id));
        });
    }

    [Test]
    public void Segment_CannotReferenceAPointInAnotherJourney()
    {
        var owner = CreateUser("River");
        var journey = CreateJourney(owner);
        var other = CreateJourney(owner);
        other.Points[1].Id = "other-return";
        other.Segments[0].ToPointId = "other-return";
        journey.Segments[0].ToPointId = "other-return";
        db.Journeys.AddRange(journey, other);

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    private static User CreateUser(string username) => new()
    {
        Username = username,
        NormalizedUsername = username.ToUpperInvariant(),
        PasswordHash = "test-only-placeholder"
    };

    private static Journey CreateJourney(User owner) => new()
    {
        Owner = owner,
        Name = "Return walk",
        Date = new DateOnly(2026, 10, 9),
        TravelMode = TravelMode.Walking,
        Objective = OptimisationObjective.Distance,
        EndMode = EndMode.ReturnToStart,
        Points =
        [
            new() { Id = "start", Position = 0, PlaceId = "same-place", Name = "Park",
                Latitude = -33.86, Longitude = 151.2, Note = "Start note" },
            new() { Id = "return", Position = 1, PlaceId = "same-place", Name = "Park",
                Latitude = -33.86, Longitude = 151.2, Note = "Return note",
                PlannedTime = new TimeOnly(15, 30) }
        ],
        Segments =
        [
            new() { Id = "leg", Position = 0, FromPointId = "start", ToPointId = "return",
                DistanceMeters = 100, DurationSeconds = 90,
                Path = [new() { Lat = -33.86, Lng = 151.2 }, new() { Lat = -33.87, Lng = 151.21 }] }
        ]
    };

    private static Invitation Invite(Journey journey, User sender, User recipient) => new()
    {
        Journey = journey,
        Sender = sender,
        Recipient = recipient,
        Status = InvitationStatus.Accepted
    };
}
