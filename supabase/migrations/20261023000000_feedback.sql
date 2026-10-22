-- 通用留言板 · Supabase 初始化脚本
-- 在 Supabase 后台 SQL Editor 里整段执行一次即可；重复执行是安全的。
-- 访客（anon / publishable key）只能“写入”留言和上传截图，不能读取任何人的留言；
-- 读取、标记处理状态只能用 service_role / secret key（只放在你本机，绝不提交到仓库）。

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

-- 不给 anon 任何 select / update / delete 策略：访客看不到别人的留言

-- 截图存储桶：私有，单张不超过 3MB，只接受常见图片格式
insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('feedback-screenshots', 'feedback-screenshots', false, 3145728, array['image/webp', 'image/jpeg', 'image/png'])
on conflict (id) do update
  set public = false, file_size_limit = 3145728, allowed_mime_types = array['image/webp', 'image/jpeg', 'image/png'];

-- 访客只能往该桶上传（路径形如 项目/留言id/1.webp），不能读取、覆盖或删除
drop policy if exists "anon can upload feedback screenshots" on storage.objects;
create policy "anon can upload feedback screenshots" on storage.objects
  for insert to anon, authenticated
  with check (
    bucket_id = 'feedback-screenshots'
    and array_length(storage.foldername(name), 1) = 2
  );
