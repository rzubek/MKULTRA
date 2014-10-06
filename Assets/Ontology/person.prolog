has_property(human, given_name).
has_property(human, surname).
has_property(human, gender).
%copular_property(gender).
has_property(human, job).
%copular_property(job).
has_property(human, age).
%copular_property(age).

has_relation(human, knows_about).
has_relation(human, interested_in).
%copular_relation([interested, in], interested_in).
has_relation(human, member_of).
%copular_relation([a, member, of], member_of).
has_relation(human, friend_of).
%copular_relation([a, friend, of], friend_of).
has_relation(human, roommate_of).
%copular_relation([a, roommate, of], roommate_of).
%copular_relation([the, roommate, of], roommate_of).
has_relation(human, knows).
has_relation(human, likes).
has_relation(human, loves).
has_relation(human, hates).

implies_relation(interested_in, knows_about).
implies_relation(loves, friend_of).
implies_relation(friend_of, likes).
implies_relation(knows, knows_about).
implies_relation(roommate_of, knows).
implies_relation(likes, knows).
implies_relation(member_of, knows_about).


