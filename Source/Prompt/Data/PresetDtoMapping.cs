using System;
using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Prompt;

public static class PresetDtoMapping
{
    public static PresetDto FromPreset(PromptPreset preset)
    {
        if (preset == null) return null;

        var dto = new PresetDto
        {
            Version = 1,
            Name = preset.Name,
            Description = preset.Description,
            Entries = new List<EntryDto>()
        };

        foreach (var entry in preset.Entries)
            dto.Entries.Add(FromEntry(entry));

        return dto;
    }

    public static PromptPreset ToPreset(PresetDto dto)
    {
        var preset = new PromptPreset
        {
            Id = Guid.NewGuid().ToString(),
            Name = dto.Name ?? "Imported Preset",
            Description = dto.Description ?? "",
            IsActive = false,
            Entries = new List<PromptEntry>()
        };

        if (dto.Entries == null)
            return preset;

        foreach (var entryDto in dto.Entries)
        {
            var entry = ToEntry(entryDto);
            if (entry == null)
                continue;
            if (!entry.IsMainChatHistory && entry.Content?.Trim() == "{{chat.history}}")
                entry.IsMainChatHistory = true;
            preset.Entries.Add(entry);
        }

        return preset;
    }

    public static EntryDto FromEntry(PromptEntry entry)
    {
        if (entry == null) return null;

        return new EntryDto
        {
            Name = entry.Name,
            Content = entry.Content,
            Role = entry.Role.ToString(),
            CustomRole = entry.CustomRole,
            Position = entry.Position.ToString(),
            InChatDepth = entry.InChatDepth,
            Enabled = entry.Enabled,
            IsMainChatHistory = entry.IsMainChatHistory
        };
    }

    public static PromptEntry ToEntry(EntryDto dto)
    {
        var entry = new PromptEntry
        {
            Id = Guid.NewGuid().ToString(),
            Name = dto.Name ?? "New Prompt",
            Content = dto.Content ?? "",
            InChatDepth = dto.InChatDepth,
            Enabled = dto.Enabled,
            IsMainChatHistory = dto.IsMainChatHistory,
            SourceModId = null
        };

        if (!string.IsNullOrWhiteSpace(dto.CustomRole))
        {
            entry.CustomRole = dto.CustomRole;
            entry.Role = PromptRole.User;
        }
        else if (!string.IsNullOrEmpty(dto.Role))
        {
            if (Enum.TryParse<PromptRole>(dto.Role, true, out var role))
                entry.Role = role;
            else
            {
                entry.CustomRole = dto.Role;
                entry.Role = PromptRole.User;
            }
        }

        if (!string.IsNullOrEmpty(dto.Position) && Enum.TryParse<PromptPosition>(dto.Position, true, out var pos))
            entry.Position = pos;

        return entry;
    }
}
