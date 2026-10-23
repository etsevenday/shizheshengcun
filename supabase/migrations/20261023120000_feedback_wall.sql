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
