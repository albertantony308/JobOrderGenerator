import { createClient } from '@supabase/supabase-js'

const supabaseUrl = import.meta.env.VITE_SUPABASE_URL || 'https://qcmcoofnxqzyrrbcwdde.supabase.co'
const supabaseKey = import.meta.env.VITE_SUPABASE_SERVICE_ROLE_KEY || import.meta.env.VITE_SUPABASE_ANON_KEY || 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InFjbWNvb2ZueHF6eXJyYmN3ZGRlIiwicm9sZSI6ImFub24iLCJpYXQiOjE3Nzg1NTE0MDYsImV4cCI6MjA5NDEyNzQwNn0.BDse0v5cLXNT9wK9K7bOkLOkyZhPJL9HpZQuFA5fixY'
export const supabase = createClient(supabaseUrl, supabaseKey)

