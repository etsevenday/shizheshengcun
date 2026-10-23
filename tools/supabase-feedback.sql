-- 通用留言板 · Supabase 初始化脚本
-- 在 Supabase 后台 SQL Editor 里整段执行一次即可；重复执行是安全的。
-- 访客（anon / publishable key）只能“写入”留言和上传截图，不能直接读 feedback 表；
-- 公开读取走 20261023120000_feedback_wall.sql 建的 feedback_wall 视图（只暴露公开字段）。
-- 标记处理状态只能用 service_role / secret key（只放在你本机，绝不提交到仓库）。

create table if not exists public.feedback (
  id              uuid primary key,
  created_at      timestamptz not null default now(),
  project         text not null,
  category        text not null default 'other',
  message         text not null default '',
  contact         text,
  app_version     text,
  page_url        text,
  user_agent      text,
  context         jsonb,
  attachment_name text,
  attachment      text,
  screenshots     text[] not null default '{}',
  status          text not null default 'new',   -- new / doing / done / wontfix
  note            text,                          -- 作者处理备注
  constraint feedback_project_len   check (char_length(project) between 1 and 64),
  constraint feedback_category_ok   check (category in ('bug', 'ux', 'balance', 'idea', 'other')),
  constraint feedback_message_len   check (char_length(message) <= 5000),
  constraint feedback_contact_len   check (contact is null or char_length(contact) <= 40),
  constraint feedback_attach_len    check (attachment is null or char_length(attachment) <= 600000),
  constraint feedback_context_size  check (context is null or pg_column_size(context) <= 200000),
  constraint feedback_shots_count   check (cardinality(screenshots) <= 6),
  constraint feedback_status_ok     check (status in ('new', 'doing', 'done', 'wontfix'))
);

create index if not exists feedback_project_created on public.feedback (project, created_at desc);

alter table public.feedback enable row level security;

-- 访客只能插入新留言，且插入时状态必须是 new、不能带处理备注
drop policy if exists "anon can submit feedback" on public.feedback;
create policy "anon can submit feedback" on public.feedback
  for insert to anon, authenticated
  with check (status = 'new' and note is null);

-- 不给 anon 任何表级 select / update / delete 策略：公开读取只能走 feedback_wall 视图

-- 截图存储桶：单张不超过 3MB，只接受常见图片格式（是否公开由 feedback_wall 脚本决定，这里不改动）
insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('feedback-screenshots', 'feedback-screenshots', false, 3145728, array['image/webp', 'image/jpeg', 'image/png'])
on conflict (id) do update
  set file_size_limit = 3145728, allowed_mime_types = array['image/webp', 'image/jpeg', 'image/png'];

-- 访客只能往该桶上传（路径形如 项目/留言id/1.webp），不能读取、覆盖或删除
drop policy if exists "anon can upload feedback screenshots" on storage.objects;
create policy "anon can upload feedback screenshots" on storage.objects
  for insert to anon, authenticated
  with check (
    bucket_id = 'feedback-screenshots'
    and array_length(storage.foldername(name), 1) = 2
  );

-- 公开留言墙 · 让所有访客都能看到全部留言和处理状态
-- 在 Supabase 后台 SQL Editor 里整段执行一次即可；重复执行是安全的。
-- 原则：feedback 表本身仍不给访客 select（里面有存档、浏览器信息等不该公开的字段），
--       访客只能通过下面这个视图读取精选字段：留言内容、称呼、分类、处理状态、作者备注、截图。

-- 视图以创建者（postgres）身份读取 feedback 表，绕过表上的行级安全，只暴露列出的字段
create or replace view public.feedback_wall
  with (security_invoker = false) as
  select id, created_at, project, category, message, contact, status, note, screenshots, app_version
  from public.feedback;

comment on view public.feedback_wall is '留言墙：访客可读的留言公开字段（不含存档、页面地址、浏览器信息）';

grant select on public.feedback_wall to anon, authenticated;

-- 截图桶改为公开读取：路径里带留言 id（随机 uuid），只能顺着留言墙里的链接看到
update storage.buckets set public = true where id = 'feedback-screenshots';

drop policy if exists "anyone can view feedback screenshots" on storage.objects;
create policy "anyone can view feedback screenshots" on storage.objects
  for select to anon, authenticated
  using (bucket_id = 'feedback-screenshots');
