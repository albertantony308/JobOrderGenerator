import { createClient } from '@supabase/supabase-js'

// These should be replaced with actual credentials provided by the user or fetched from env
const supabaseUrl = 'https://qcmcoofnxqzyrrbcwdde.supabase.co'
const supabaseKey = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InFjbWNvb2ZueHF6eXJyYmN3ZGRlIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImlhdCI6MTc3ODU1MTQwNiwiZXhwIjoyMDk0MTI3NDA2fQ.dCScjM5qRCFY1BVLJcl6gVcXQjEGtibo4LsD7aTMCF8'
export const supabase = createClient(supabaseUrl, supabaseKey)
