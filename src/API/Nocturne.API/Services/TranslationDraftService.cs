using Microsoft.EntityFrameworkCore;
using Nocturne.API.Extensions;
using Nocturne.Core.Contracts.Translations;
using Nocturne.Core.Models.Translations;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Mappers;

namespace Nocturne.API.Services;

/// <summary>
/// Server-side storage for the current user's in-progress translations.
/// Tenant isolation comes from the RLS global query filter; this service only
/// scopes by subject. Uniqueness of (subject, locale, context, msgid) is
/// enforced here by the upsert because msgid is unbounded text and cannot be
/// part of a btree unique index.
/// </summary>
public class TranslationDraftService(
    NocturneDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    ITranslationContributionService contributionService) : ITranslationDraftService
{
    private Guid SubjectId => httpContextAccessor.HttpContext!.GetSubjectId()!.Value;
    private Guid TenantId => httpContextAccessor.HttpContext!.GetAuthContext().TenantId!.Value;

    public async Task<IReadOnlyList<TranslationDraft>> GetDraftsAsync(
        string locale, CancellationToken ct = default)
    {
        var subjectId = SubjectId;
        var entities = await dbContext.TranslationDrafts
            .AsNoTracking()
            .Where(d => d.SubjectId == subjectId && d.Locale == locale)
            .OrderBy(d => d.UpdatedAt)
            .ToListAsync(ct);
        return entities.Select(TranslationDraftMapper.ToDomainModel).ToList();
    }

    public async Task<IReadOnlyList<TranslationDraft>> UpsertDraftsAsync(
        string locale, IReadOnlyList<TranslationEntryDto> entries, CancellationToken ct = default)
    {
        var subjectId = SubjectId;
        var existing = await dbContext.TranslationDrafts
            .Where(d => d.SubjectId == subjectId && d.Locale == locale)
            .ToListAsync(ct);
        var byKey = existing.ToDictionary(d => (d.Context, d.MsgId));
        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            var key = (entry.Context ?? "", entry.MsgId);

            if (entry.Translations.Count == 0)
            {
                if (byKey.TryGetValue(key, out var toDelete))
                {
                    dbContext.TranslationDrafts.Remove(toDelete);
                    byKey.Remove(key);
                }
                continue;
            }

            if (byKey.TryGetValue(key, out var draft))
            {
                draft.Translations = entry.Translations;
                draft.UpdatedAt = now;
            }
            else
            {
                var created = new TranslationDraftEntity
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = TenantId,
                    SubjectId = subjectId,
                    Locale = locale,
                    Context = entry.Context ?? "",
                    MsgId = entry.MsgId,
                    Translations = entry.Translations,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                dbContext.TranslationDrafts.Add(created);
                byKey[key] = created;
            }
        }

        await dbContext.SaveChangesAsync(ct);
        return byKey.Values
            .OrderBy(d => d.UpdatedAt)
            .Select(TranslationDraftMapper.ToDomainModel)
            .ToList();
    }

    public async Task<int> ClearDraftsAsync(string locale, CancellationToken ct = default)
    {
        var subjectId = SubjectId;
        var drafts = await dbContext.TranslationDrafts
            .Where(d => d.SubjectId == subjectId && d.Locale == locale)
            .ToListAsync(ct);
        dbContext.TranslationDrafts.RemoveRange(drafts);
        await dbContext.SaveChangesAsync(ct);
        return drafts.Count;
    }

    public async Task<TranslationDraftSubmitResult> SubmitDraftsAsync(
        string locale, TranslationContributorDto contributor, string? note, CancellationToken ct = default)
    {
        var subjectId = SubjectId;
        var drafts = await dbContext.TranslationDrafts
            .Where(d => d.SubjectId == subjectId && d.Locale == locale)
            .ToListAsync(ct);

        if (drafts.Count == 0)
            throw new TranslationContributionRejectedException("There are no drafts to submit for this locale.");

        var request = new TranslationContributionRequest
        {
            Locale = locale,
            Entries = drafts.Select(d => new TranslationEntryDto
            {
                MsgId = d.MsgId,
                Context = d.Context.Length == 0 ? null : d.Context,
                Translations = d.Translations,
            }).ToList(),
            Contributor = contributor,
            Note = note,
        };

        var response = contributionService.HasLocalPat
            ? await contributionService.SubmitAsync(request, ct)
            : await contributionService.RelayAsync(request, ct);

        // Applied drafts are done; unmatched ones (message no longer in the
        // catalog) are kept so the work is not silently lost.
        var unmatched = response.Unmatched.ToHashSet(StringComparer.Ordinal);
        var applied = drafts.Where(d => !unmatched.Contains(d.MsgId)).ToList();
        dbContext.TranslationDrafts.RemoveRange(applied);
        await dbContext.SaveChangesAsync(ct);

        return new TranslationDraftSubmitResult
        {
            Contribution = response,
            RemainingDrafts = drafts.Count - applied.Count,
        };
    }
}
