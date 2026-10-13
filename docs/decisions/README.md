# Архитектурные решения

ADR фиксируют контекст решения на момент его принятия. Актуальные инструкции по
использованию находятся уровнем выше в каталоге `docs`.

| ADR | Решение | Статус |
| --- | --- | --- |
| [ADR-001](ADR-001-Platform-Agnostic-Architecture.md) | Платформонезависимое ядро и адаптеры | Accepted |
| [ADR-002](ADR-002-Keyed-DI-Command-Routing.md) | Keyed DI и конвейер диспетчера | Superseded in part by ADR-004 |
| [ADR-003](ADR-003-Finite-State-Machine-Session-Storage.md) | FSM и хранилище сессий | Superseded in part by ADR-005 and ADR-006 |
| [ADR-004](ADR-004-Explicit-Commands-Before-Sessions.md) | Приоритет явных команд над сессией | Accepted |
| [ADR-005](ADR-005-Scoped-Session-Keys.md) | Область ключей сессии | Accepted |
| [ADR-006](ADR-006-Fail-Fast-Redis-Sessions.md) | Fail-fast поведение Redis | Accepted |

Новый ADR должен содержать статус, дату, контекст, принятое решение, рассмотренные
альтернативы и последствия.
