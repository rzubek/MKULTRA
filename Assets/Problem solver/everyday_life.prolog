%%
%% The "everyday life" task.
%%
%% Periodically searches for stuff to do.
%%

character_initialization :-
   start_task(everyday_life, 100).

strategy(everyday_life,
	 ( achieve(P), everyday_life )) :-
   maintenance_goal(P),
   \+ P,
   % Make sure that P isn't obviously unachievable.
   once(strategy(achieve(P), _)).

default_strategy(everyday_life,
		 ( wait_event_with_timeout(_, 2),
		   call(assert(/physiological_states/thirsty)),
		   everyday_life )).

%default_strategy(everyday_life,
%		 ( wait_event_with_timeout(_, 2),
%		   everyday_life )).

%default_strategy(everyday_life,
%		 ( wait_event_with_timeout(_, 2),
%		   maybe_phys_state(B, thirsty),
%		   everyday_life )) :-
%	once(random_member(B, [true, false])).

strategy(maybe_phys_state(B, State), 
		 call(assert(/physiological_states/State))) :-
	B = true.					% update if b is true

strategy(maybe_phys_state(B, _), 
		null) :-
	B = false.					% don't do anything on false


maintenance_goal(~hungry($me)).
hungry($me) :- /physiological_states/hungry.
~hungry(X) :- \+ hungry(X).

maintenance_goal(~thirsty($me)).
thirsty($me) :- /physiological_states/thirsty.
~thirsty(X) :- \+ thirsty(X).

maintenance_goal(~tired($me)).
tired($me) :- /physiological_states/tired.
~tired(X) :- \+ tired(X).

maintenance_goal(~dirty($me)).
dirty($me) :- /physiological_states/dirty.
~dirty(X) :- \+ dirty(X).

maintenance_goal(~full_bladder($me)).
full_bladder($me) :- /physiological_states/full_bladder.
~full_bladder(X) :- \+ full_bladder(X).
