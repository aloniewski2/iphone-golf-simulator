-- Golf Arcade: players and their finished rounds. Applied at start-up; safe to run again.

create table if not exists players (
  id          uuid primary key,
  name        text        not null,
  body        smallint    not null default 0,
  kit         smallint    not null default 0,   -- GolferStyle.KitColors
  shirt       smallint    not null default 0,   -- GolferStyle.ShirtColors
  token_hash  text        not null unique,
  stats       jsonb       not null default '{}'::jsonb,
  created_at  timestamptz not null default now(),
  updated_at  timestamptz not null default now()
);

create table if not exists rounds (
  id          bigserial   primary key,
  player_id   uuid        not null references players(id) on delete cascade,
  course      text        not null,
  strokes     integer[]   not null,
  to_par      integer     not null,
  result      text,                    -- 'won' | 'tied' | 'lost', null for a solo round
  room_code   text,                    -- the online room, when it was played online
  created_at  timestamptz not null default now()
);

create index if not exists rounds_player_idx on rounds (player_id, created_at desc);
create index if not exists rounds_course_idx on rounds (course, to_par, created_at);
