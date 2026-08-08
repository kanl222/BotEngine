using BotEngine.Core.Models;

namespace BotEngine.Core.Interfaces;

/// <summary>
/// Хранилище диалоговых состояний пользователей.
/// </summary>
public interface IUserSessionStore
{
    /// <summary>
    /// Возвращает текущее состояние диалога для указанного пользователя.
    /// </summary>
    /// <param name="userId">Уникальный идентификатор пользователя.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>
    /// Объект <see cref="UserDialogState"/>, если состояние найдено;
    /// иначе <see langword="null"/>.
    /// </returns>
    Task<UserDialogState?> GetStateAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Сохраняет состояние диалога для указанного пользователя.
    /// </summary>
    /// <param name="userId">Уникальный идентификатор пользователя.</param>
    /// <param name="state">Сохраняемое состояние диалога.</param>
    /// <param name="ttl">
    /// Время жизни записи. Если <see langword="null"/>, используется
    /// значение по умолчанию, заданное в конфигурации хранилища.
    /// </param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Задача, представляющая асинхронную операцию сохранения.</returns>
    Task SetStateAsync(string userId, UserDialogState state, TimeSpan? ttl = null, CancellationToken ct = default);

    /// <summary>
    /// Удаляет состояние диалога для указанного пользователя.
    /// </summary>
    /// <param name="userId">Уникальный идентификатор пользователя.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Задача, представляющая асинхронную операцию удаления.</returns>
    Task ClearStateAsync(string userId, CancellationToken ct = default);
}
