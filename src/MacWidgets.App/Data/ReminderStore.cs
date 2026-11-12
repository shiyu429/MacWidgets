namespace MacWidgets.Data;

/// <summary>提醒事项（全局共享，任何修改都会广播 Changed，小号与计划组件同步刷新）。</summary>
public static class ReminderStore
{
    public static event Action? Changed;

    private static List<ReminderItem> Items => Store.Reminders.Items;

    public static IReadOnlyList<ReminderItem> Today()
    {
        var today = DateTime.Today;
        return Items.Where(r => !r.Completed && ((r.Due is { } d && d.Date <= today) || (r.Due == null && r.CreatedAt.Date == today)))
            .OrderBy(r => r.Due ?? DateTime.MaxValue).ThenBy(r => r.CreatedAt).ToList();
    }

    public static IReadOnlyList<ReminderItem> AllOpen() =>
        Items.Where(r => !r.Completed).OrderBy(r => r.Due ?? DateTime.MaxValue).ThenBy(r => r.CreatedAt).ToList();

    public static IReadOnlyList<ReminderItem> Scheduled()
    {
        var today = DateTime.Today;
        return Items.Where(r => !r.Completed && !IsSeedSample(r) && r.Due is { } d && d.Date >= today)
            .OrderBy(r => r.Due!.Value.Date).ThenBy(r => r.HasTime ? r.Due!.Value.TimeOfDay : TimeSpan.MaxValue).ToList();
    }

    /// <summary>
    /// 预置示例「你好」（仅属于「今天」组件）。1.0.23 及以前生成的示例没有 IsSample 标记，
    /// 按其固定特征识别：标题「你好」、当天 23:00、带时间、且创建于同一天。
    /// </summary>
    public static bool IsSeedSample(ReminderItem r) =>
        r.IsSample ||
        (r.Title == "你好" && r.HasTime && r.Due is { } d && d.TimeOfDay == TimeSpan.FromHours(23) && d.Date == r.CreatedAt.Date);

    public static ReminderItem? Find(string id) => Items.FirstOrDefault(r => r.Id == id);

    public static ReminderItem Add(string title, DateTime? due, bool hasTime)
    {
        var r = new ReminderItem { Title = title.Trim(), Due = due, HasTime = hasTime && due != null };
        Items.Add(r);
        Commit();
        return r;
    }

    public static void Update(ReminderItem r) => Commit();

    public static void Complete(string id)
    {
        var r = Find(id);
        if (r == null) return;
        r.Completed = true;
        r.CompletedAt = DateTime.Now;
        Commit();
    }

    public static void Delete(string id)
    {
        Items.RemoveAll(r => r.Id == id);
        Commit();
    }

    public static void ClearCompleted()
    {
        Items.RemoveAll(r => r.Completed);
        Commit();
    }

    public static void Commit()
    {
        Store.SaveRemindersSoon();
        Changed?.Invoke();
    }

    public static void RaiseChanged() => Changed?.Invoke();

    public static string FormatDue(ReminderItem r)
    {
        if (r.Due is not { } d) return "";
        var today = DateTime.Today;
        string day = d.Date == today ? "" : d.Date == today.AddDays(1) ? "明天" : d.Date < today ? "已过期" : $"{d.Month}月{d.Day}日";
        if (!r.HasTime || IsSeedSample(r)) return day == "" ? "今天" : day;
        return day == "" ? d.ToString("HH:mm") : $"{day} {d:HH:mm}";
    }
}
