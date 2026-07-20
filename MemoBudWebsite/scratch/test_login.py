import requests
import json

headers = {
    'apikey': 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InFjbWNvb2ZueHF6eXJyYmN3ZGRlIiwicm9sZSI6ImFub24iLCJpYXQiOjE3Nzg1NTE0MDYsImV4cCI6MjA5NDEyNzQwNn0.BDse0v5cLXNT9wK9K7bOkLOkyZhPJL9HpZQuFA5fixY',
    'Authorization': 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InFjbWNvb2ZueHF6eXJyYmN3ZGRlIiwicm9sZSI6ImFub24iLCJpYXQiOjE3Nzg1NTE0MDYsImV4cCI6MjA5NDEyNzQwNn0.BDse0v5cLXNT9wK9K7bOkLOkyZhPJL9HpZQuFA5fixY'
}

# Get all staff members
res_staff = requests.get('https://qcmcoofnxqzyrrbcwdde.supabase.co/rest/v1/staff?select=activation_key_id', headers=headers)
staff_keys = {s['activation_key_id'] for s in res_staff.json()}
print(f"Active staff key IDs: {staff_keys}")

# Get all SFK keys
res_keys = requests.get('https://qcmcoofnxqzyrrbcwdde.supabase.co/rest/v1/activation_keys?key_code=like.SFK-*', headers=headers)
sfk_keys = res_keys.json()

for k in sfk_keys:
    kid = k['id']
    if kid not in staff_keys:
        print(f"Deleting unused SFK key: {k['key_code']}")
        requests.delete(f"https://qcmcoofnxqzyrrbcwdde.supabase.co/rest/v1/activation_keys?id=eq.{kid}", headers=headers)
    else:
        print(f"Keeping active SFK key: {k['key_code']}")

